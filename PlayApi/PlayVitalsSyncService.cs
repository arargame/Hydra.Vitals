using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hydra.Vitals.Core;
using Hydra.Vitals.Models;

namespace Hydra.Vitals.PlayApi
{
    /// <summary>Bir uygulamanin belirli bir aralikta cekilmis vital ozeti.</summary>
    public sealed class PlayVitalsSnapshot
    {
        public string PackageName { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public DateOnly From { get; init; }
        public DateOnly To { get; init; }

        public List<MetricPoint> AnrRate { get; init; } = new();
        public List<MetricPoint> UserPerceivedAnrRate { get; init; } = new();
        public List<MetricPoint> CrashRate { get; init; } = new();
        public List<MetricPoint> UserPerceivedCrashRate { get; init; } = new();
        public List<MetricPoint> DistinctUsers { get; init; } = new();

        public List<ErrorIssueDto> AnrIssues { get; init; } = new();
        public List<ErrorIssueDto> CrashIssues { get; init; } = new();

        /// <summary>
        /// Son degeri veren yardimci. Google verisi 1-2 gun gecikmeli
        /// gelebildigi icin "bugun" bos olabilir; bos olmayan EN SON gun
        /// aliniyor. Bosa 0 yazmak, "cokme yok" gibi yanlis bir izlenim verir.
        /// </summary>
        public static decimal? Latest(List<MetricPoint> series)
        {
            for (int i = series.Count - 1; i >= 0; i--)
                if (series[i].Value.HasValue) return series[i].Value;
            return null;
        }

        /// <summary>Aralik boyunca kullanici sayisiyla agirliklandirilmis ortalama.</summary>
        public static decimal? WeightedAverage(List<MetricPoint> series, List<MetricPoint> users)
        {
            decimal num = 0m, den = 0m;
            var byDate = users.Where(u => u.Value.HasValue).ToDictionary(u => u.Date, u => u.Value!.Value);

            foreach (var p in series)
            {
                if (!p.Value.HasValue) continue;
                // Gunun kullanici sayisi yoksa o gun agirliksiz kalir; agirlik 1
                // vermek buyuk ve kucuk gunleri esitler ve sonucu bozar.
                if (!byDate.TryGetValue(p.Date, out var w) || w <= 0) continue;
                num += p.Value.Value * w;
                den += w;
            }

            return den > 0 ? num / den : null;
        }
    }

    /// <summary>
    /// Play Reporting API'den crash ve ANR verisini ceker, istege bagli olarak
    /// mevcut bilgi bankasina (VitalIssue) yazar.
    ///
    /// KAPSAM BILEREK DAR: yalnizca crash ve ANR. Buyuk Play Manager projesi
    /// (bkz. docs/ARAR_GAMES_PLAY_MANAGER_SPEC.md) ileride yapilacak; bugun
    /// gereken sey oyunun coken/donan yerlerini gormek.
    /// </summary>
    public sealed class PlayVitalsSyncService
    {
        private readonly PlayReportingClient _client;

        public PlayVitalsSyncService(PlayReportingClient client) => _client = client;

        public Task<List<PlayAppDto>> GetAppsAsync(CancellationToken ct = default)
            => _client.SearchAppsAsync(ct);

        /// <summary>
        /// Aralik hesabi. Bitis gunu icin "dun" varsayilani kullaniliyor;
        /// asil kirpma istemcide TAZELIK degerine gore yapiliyor (bkz.
        /// PlayReportingClient.GetFreshnessAsync).
        /// </summary>
        public static (DateOnly From, DateOnly To) Range(int days)
        {
            var to = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            var from = to.AddDays(-Math.Max(1, days) + 1);
            return (from, to);
        }

        /// <summary>Yalnizca ORANLAR (ANR + crash rate). Iki istek.</summary>
        public async Task<PlayVitalsSnapshot> FetchRatesAsync(
            PlayAppDto app, int days, CancellationToken ct = default)
        {
            var (from, to) = Range(days);

            var anr = await _client.QueryAnrRateAsync(app.PackageName, from, to, ct);
            var crash = await _client.QueryCrashRateAsync(app.PackageName, from, to, ct);

            return new PlayVitalsSnapshot
            {
                PackageName = app.PackageName,
                DisplayName = app.DisplayName,
                From = from,
                To = to,
                AnrRate = Get(anr, "anrRate"),
                UserPerceivedAnrRate = Get(anr, "userPerceivedAnrRate"),
                CrashRate = Get(crash, "crashRate"),
                UserPerceivedCrashRate = Get(crash, "userPerceivedCrashRate"),
                DistinctUsers = Get(anr, "distinctUsers"),
            };
        }

        /// <summary>
        /// Yalnizca KONULAR - tek tur icin (ANR ya da CRASH). Tek istek.
        ///
        /// NIYE AYRI: oran sorgulari her metrik seti icin ayri bir tazelik
        /// istegi + ayri bir sorgu demek, yani dort HTTP cagrisi. Sadece
        /// "hangi ANR'ler var" ogrenmek isteyen biri icin bu bosuna maliyet.
        /// Play Console'un "Crashes and ANRs" listesi de zaten konulari
        /// gosteriyor, oranlari degil.
        /// </summary>
        public async Task<PlayVitalsSnapshot> FetchIssuesAsync(
            PlayAppDto app, int days, bool anr, CancellationToken ct = default)
        {
            var (from, to) = Range(days);

            var issues = await _client.SearchErrorIssuesAsync(
                app.PackageName,
                anr ? "ANR" : "CRASH",
                from, to, 50, ct);

            return new PlayVitalsSnapshot
            {
                PackageName = app.PackageName,
                DisplayName = app.DisplayName,
                From = from,
                To = to,
                AnrIssues = anr ? issues : new List<ErrorIssueDto>(),
                CrashIssues = anr ? new List<ErrorIssueDto>() : issues,
            };
        }

        /// <summary>
        /// Bir konuya ait hata raporlari (yigin izi ve cihaz bilgileri).
        /// </summary>
        public Task<List<ErrorReportDto>> FetchReportsAsync(
            PlayAppDto app, string? issueName, int days, int maxResults = 5, CancellationToken ct = default)
        {
            var (from, to) = Range(days);
            return _client.SearchErrorReportsAsync(app.PackageName, issueName, from, to, maxResults, ct);
        }

        private static List<MetricPoint> Get(Dictionary<string, List<MetricPoint>> d, string key)
            => d.TryGetValue(key, out var v) ? v : new List<MetricPoint>();

        // =====================================================================
        // BILGI BANKASINA YAZMA
        // =====================================================================

        /// <summary>
        /// Google'dan gelen konulari VitalIssue'ya cevirir.
        ///
        /// NIYE OTOMATIK YAZMIYORUZ (cagiran acikca istemeli):
        /// Bu bilgi bankasinin degeri KOK NEDEN ve COZUM alanlarinda - onlari
        /// insan yaziyor. Google'dan gelen ham konulari otomatik doldurmak,
        /// bankayi bos govdelerle sisirir ve gercek kayitlari goze batmaz hale
        /// getirir.
        ///
        /// Kod alani "PLAY-" onekiyle uretiliyor ki elle girilen kayitlardan
        /// ayirt edilebilsin.
        /// </summary>
        public static List<VitalIssue> ToVitalIssues(
            PlayVitalsSnapshot snapshot, Guid projectId, string projectName,
            IReadOnlyDictionary<string, List<ErrorReportDto>>? reportsByIssue = null)
        {
            var list = new List<VitalIssue>();

            void Add(ErrorIssueDto i, VitalType type)
            {
                // Google kaynak adi "apps/{pkg}/errorIssues/{id}" bicimde;
                // son parca kararli bir kimlik.
                var id = i.Name.Contains('/') ? i.Name[(i.Name.LastIndexOf('/') + 1)..] : i.Name;
                var code = "PLAY-" + (type == VitalType.ANR ? "ANR-" : "CRASH-") + id;

                var title = !string.IsNullOrWhiteSpace(i.Cause) ? i.Cause!
                          : !string.IsNullOrWhiteSpace(i.Location) ? i.Location!
                          : (type == VitalType.ANR ? "ANR" : "Crash") + " " + id;

                var issue = new VitalIssue(
                    code, title, projectId, projectName, type,
                    subtype: i.Type ?? string.Empty,
                    severity: Severity(i.DistinctUsers),
                    status: VitalStatus.Open)
                {
                    EventCount = (int)Math.Min(int.MaxValue, i.ErrorReportCount),
                    AffectedUsers = (int)Math.Min(int.MaxValue, i.DistinctUsers),
                    ReportedVersion = i.LastAppVersion,
                    RootCause = string.Empty,      // insan doldurur
                    FixApproach = string.Empty,    // insan doldurur
                    AiDiagnosticsGuidance =
                        "Play Console'dan otomatik cekildi. Konum: " + (i.Location ?? "bilinmiyor")
                        + ". Son rapor: " + (i.LastErrorReportTime ?? "bilinmiyor")
                        + ". Surum araligi: " + (i.FirstAppVersion ?? "?") + " - " + (i.LastAppVersion ?? "?")
                        + (!string.IsNullOrWhiteSpace(i.IssueUri) ? ". Play Console: " + i.IssueUri : "")
                        + ". Kok neden ve cozum ELLE doldurulmali.",
                };

                if (!string.IsNullOrWhiteSpace(i.Location))
                    issue.SignatureFrames.Add(i.Location!);

                // AYRINTI: yigin izi ve cihaz bilgisi.
                //
                // Bu olmadan bilgi bankasi yalnizca "su konu su kadar
                // kullaniciyi etkiledi" diyor - yani sayac. Bankayi degerli
                // yapan sey IMZA: bir dahaki sefere ayni izi gorunce eski
                // cozume saniyeler icinde ulasmak.
                if (reportsByIssue != null && reportsByIssue.TryGetValue(i.Name, out var reports) && reports.Count > 0)
                {
                    var first = reports[0];
                    issue.FullStackTrace = first.ReportText;

                    // Imza kareleri: metnin ilk anlamli satirlari. Google
                    // "bu metni ayristirmayin, bicimi degisebilir" diyor -
                    // o yuzden AYRISTIRMIYORUZ, sadece ilk satirlari
                    // kirpiyoruz. Bicim degisse bile bu bozulmaz.
                    foreach (var frame in TopFrames(first.ReportText, 6))
                        if (!issue.SignatureFrames.Contains(frame))
                            issue.SignatureFrames.Add(frame);

                    foreach (var r in reports)
                    {
                        if (string.IsNullOrWhiteSpace(r.DeviceModel)) continue;

                        var dev = new VitalDevice(
                            model: r.DeviceMarketingName ?? r.DeviceModel!,
                            manufacturer: r.DeviceBrand ?? "?",
                            androidVersion: r.OsVersion ?? "?",
                            apiLevel: int.TryParse(r.OsVersion, out var api) ? api : null);

                        if (!issue.Devices.Any(d => d.Model == dev.Model && d.AndroidVersion == dev.AndroidVersion))
                            issue.Devices.Add(dev);
                    }

                    if (!string.IsNullOrWhiteSpace(first.EventTime)
                        && DateTime.TryParse(first.EventTime, out var evt))
                        issue.DetectedDate = evt.ToUniversalTime();
                }

                list.Add(issue);
            }

            foreach (var i in snapshot.AnrIssues) Add(i, VitalType.ANR);
            foreach (var i in snapshot.CrashIssues) Add(i, VitalType.Crash);

            return list;
        }

        /// <summary>
        /// Rapor metninin ilk anlamli satirlari — "imza".
        ///
        /// Google reportText icin "makine tuketimi icin tasarlanmadi, bicimi
        /// degisebilir" uyarisi yapiyor. Bu yuzden AYRISTIRMA yok: satir
        /// bolup bos olmayan ilk N tanesini aliyoruz. Bicim degisse bile
        /// bu yaklasim bozulmaz, en fazla daha az yararli olur.
        /// </summary>
        private static IEnumerable<string> TopFrames(string? reportText, int count)
        {
            if (string.IsNullOrWhiteSpace(reportText)) yield break;

            int taken = 0;
            foreach (var raw in reportText.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;

                yield return line.Length > 200 ? line[..200] : line;
                if (++taken >= count) yield break;
            }
        }

        /// <summary>
        /// Etkilenen kullanici sayisindan kaba bir siddet.
        ///
        /// Esikler kesin bilim degil; amaci listeyi siralamak. Gercek siddeti
        /// insan belirler - bu yuzden yazilan kayitlar Open durumunda ve kok
        /// nedeni bos birakiliyor.
        /// </summary>
        private static VitalSeverity Severity(long distinctUsers) => distinctUsers switch
        {
            >= 1000 => VitalSeverity.Critical,
            >= 200 => VitalSeverity.High,
            >= 20 => VitalSeverity.Medium,
            > 0 => VitalSeverity.Low,
            _ => VitalSeverity.None,
        };
    }
}
