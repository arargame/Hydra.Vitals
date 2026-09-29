using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Hydra.Vitals.Data;
using Hydra.Vitals.Models;
using Hydra.Vitals.PlayApi;

namespace Hydra.Vitals.UI
{
    /// <summary>
    /// Google Play'den crash ve ANR cekme ekrani.
    ///
    /// Mevcut ConsoleDashboard elle girilmis bilgi bankasini gosteriyor; bu
    /// ekran ise CANLI veriyi getiriyor. Ikisi bilerek ayri: biri kurum
    /// hafizasi, digeri gunun durumu.
    /// </summary>
    public sealed class PlayVitalsMenu
    {
        private readonly IVitalRepository<VitalIssue> _issueRepo;
        private readonly IVitalRepository<AppProject> _projectRepo;

        private PlayVitalsSyncService? _service;
        private PlayStoreSyncService? _storeSyncService;
        private IGoogleTokenService? _tokens;
        private List<PlayAppDto>? _apps;

        public PlayVitalsMenu(IVitalRepository<VitalIssue> issueRepo, IVitalRepository<AppProject> projectRepo)
        {
            _issueRepo = issueRepo;
            _projectRepo = projectRepo;
        }

        public async Task RunAsync()
        {
            var options = PlayApiOptions.Load();
            if (options == null) { PrintSetupHelp(); Pause(); return; }

            // Tek HttpClient: her istekte yenisini yaratmak soket tukenmesine
            // yol acar (klasik SocketException tuzagi).
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

            ITokenProtector protector = DpapiTokenProtector.IsSupported
                ? new DpapiTokenProtector()
                : new NoPersistTokenProtector();

            _tokens = new GoogleTokenService(options, protector, http);
            _service = new PlayVitalsSyncService(new PlayReportingClient(_tokens, http));
            _storeSyncService = new PlayStoreSyncService(new PlayPublisherClient(_tokens, http));

            if (!DpapiTokenProtector.IsSupported)
            {
                Console.WriteLine("NOT: Bu platformda jeton sifreli saklanamiyor, bu yuzden");
                Console.WriteLine("     saklanmiyor. Her calistirmada yeniden giris istenecek.");
                Console.WriteLine();
            }

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("=== GOOGLE PLAY — CRASH, ANR & STORE PRESENCE ===");
                Console.WriteLine("1) Uygulamalari listele");
                Console.WriteLine("2) Uygulama sec -> ANR / Crash / Oranlar");
                Console.WriteLine("3) Magaza Sayfalarini Esitle (YouTube & Gorseller -> Tum Diller)");
                Console.WriteLine("4) Google oturumu yenile (Publisher & Reporting izinleri)");
                Console.WriteLine("5) Google oturumunu kapat");
                Console.WriteLine("0) Geri");
                Console.Write("> ");

                switch ((Console.ReadLine() ?? "").Trim())
                {
                    case "1": await SafeAsync(ListAppsAsync); break;
                    case "2": await SafeAsync(FetchFlowAsync); break;
                    case "3": await SafeAsync(SyncStoreFlowAsync); break;
                    case "4": await SafeAsync(ReauthorizeAsync); break;
                    case "5": await SafeAsync(LogoutAsync); break;
                    case "0": return;
                }
            }
        }

        /// <summary>
        /// Google hatalarini kullanicinin anlayacagi bicimde gosterir.
        ///
        /// Ham istisnayi basmak burada ise yaramaz: 403 gordugunde asil
        /// bilinmesi gereken sey "hangi izin eksik", stack trace degil.
        /// </summary>
        private static async Task SafeAsync(Func<Task> action)
        {
            try { await action(); }
            catch (GoogleApiException ex)
            {
                Console.WriteLine();
                Console.WriteLine("Google API hatasi (" + (int)ex.StatusCode + "):");
                Console.WriteLine(ex.FriendlyMessage);
                if (!string.IsNullOrWhiteSpace(ex.Message))
                {
                    Console.WriteLine("Google Detay Mesaji: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Hata: " + ex.Message);
            }
            Pause();
        }

        private async Task ListAppsAsync()
        {
            Console.WriteLine("Uygulamalar getiriliyor...");
            _apps = await _service!.GetAppsAsync();

            if (_apps.Count == 0)
            {
                Console.WriteLine("Hicbir uygulama donmedi.");
                Console.WriteLine("Bu genelde su demektir: Google hesabin Play Console'da bu");
                Console.WriteLine("uygulamalara bagli degil, ya da Reporting API'ye erisim izni yok.");
                return;
            }

            Console.WriteLine();
            for (int i = 0; i < _apps.Count; i++)
                Console.WriteLine($"{i + 1,3}) {_apps[i].DisplayName}  [{_apps[i].PackageName}]");
        }

        /// <summary>
        /// Uygulama sec -> ne istedigini sec.
        ///
        /// NIYE IKI ADIM: her sey tek seferde cekiliyordu ve bu dort HTTP
        /// cagrisi demekti (iki tazelik + iki oran) ustune iki konu sorgusu.
        /// Cogu zaman istenen sey tek bir liste: "hangi ANR'ler var".
        /// Play Console'un kendisi de boyle calisiyor - once uygulama, sonra
        /// Crashes and ANRs.
        /// </summary>
        private async Task FetchFlowAsync()
        {
            if (_apps == null || _apps.Count == 0) await ListAppsAsync();
            if (_apps == null || _apps.Count == 0) return;

            Console.Write("Uygulama numarasi: ");
            if (!int.TryParse(Console.ReadLine(), out var idx) || idx < 1 || idx > _apps.Count) return;
            var app = _apps[idx - 1];

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine($"--- {app.DisplayName} [{app.PackageName}] ---");
                Console.WriteLine("1) ANR listesi");
                Console.WriteLine("2) Crash listesi");
                Console.WriteLine("3) Oranlar (ANR + crash rate)");
                Console.WriteLine("0) Geri");
                Console.Write("> ");

                var choice = (Console.ReadLine() ?? "").Trim();
                if (choice == "0") return;

                if (choice != "1" && choice != "2" && choice != "3") continue;

                int days = AskDays();

                if (choice == "3")
                {
                    Console.WriteLine($"Oranlar getiriliyor ({days} gun)...");
                    var rates = await _service!.FetchRatesAsync(app, days);
                    PrintRates(rates);
                    continue;
                }

                bool anr = choice == "1";
                Console.WriteLine($"{(anr ? "ANR" : "Crash")} listesi getiriliyor ({days} gun)...");

                var snap = await _service!.FetchIssuesAsync(app, days, anr);
                var issues = anr ? snap.AnrIssues : snap.CrashIssues;

                PrintIssues(anr ? "ANR KONULARI" : "CRASH KONULARI", issues);

                if (issues.Count == 0) continue;

                Console.WriteLine();
                Console.WriteLine("d) Bir konunun YIGIN IZINI gor   (numara girerek)");
                Console.WriteLine("y) Hepsini bilgi bankasina yaz   (yigin izleriyle birlikte)");
                Console.Write("> ");

                var after = (Console.ReadLine() ?? "").Trim();

                if (after.Equals("y", StringComparison.OrdinalIgnoreCase))
                {
                    var reports = await FetchAllReportsAsync(app, issues, days);
                    await ImportAsync(snap, reports);
                }
                else if (after.Equals("d", StringComparison.OrdinalIgnoreCase))
                {
                    await ShowIssueDetailAsync(app, issues, days);
                }
            }
        }

        /// <summary>
        /// Tek bir konunun yigin izini gosterir.
        ///
        /// AYRINTI NIYE AYRI BIR ADIM: her konu icin rapor cekmek ayri bir
        /// HTTP istegi. Listede on bes konu varken hepsini pesin cekmek on
        /// bes istek demek - cogu bakilmadan cope gidiyor.
        /// </summary>
        private async Task ShowIssueDetailAsync(PlayAppDto app, List<ErrorIssueDto> issues, int days)
        {
            Console.Write("Konu numarasi (1-" + issues.Count + "): ");
            if (!int.TryParse(Console.ReadLine(), out var n) || n < 1 || n > issues.Count) return;

            var issue = issues[n - 1];
            Console.WriteLine();
            Console.WriteLine("Rapor getiriliyor...");

            var reports = await _service!.FetchReportsAsync(app, issue.Name, days, 3);

            if (reports.Count == 0)
            {
                Console.WriteLine("Bu konu icin rapor donmedi.");
                Console.WriteLine("Ornek raporlar her konu icin saklanmayabiliyor;");
                Console.WriteLine("cok eski ya da cok seyrek konularda bos donuyor.");
                return;
            }

            foreach (var r in reports)
            {
                Console.WriteLine();
                Console.WriteLine(new string('=', 78));
                Console.WriteLine($"Cihaz : {r.DeviceMarketingName ?? "?"} ({r.DeviceBrand}/{r.DeviceModel})");
                Console.WriteLine($"Android API : {r.OsVersion ?? "?"}    Surum : {r.AppVersion ?? "?"}");
                Console.WriteLine($"Zaman : {r.EventTime ?? "?"}");
                Console.WriteLine(new string('-', 78));
                Console.WriteLine(r.ReportText ?? "(metin yok)");
            }
        }

        /// <summary>
        /// Yazma oncesi TUM konularin raporlarini ceker.
        ///
        /// Maliyet acik: konu basina bir istek. Kullanici bunu bilerek
        /// seciyor - "hepsini yaz" dedigi an zaten ayrinti istiyor demektir.
        /// </summary>
        private async Task<Dictionary<string, List<ErrorReportDto>>> FetchAllReportsAsync(
            PlayAppDto app, List<ErrorIssueDto> issues, int days)
        {
            var map = new Dictionary<string, List<ErrorReportDto>>(StringComparer.Ordinal);

            for (int i = 0; i < issues.Count; i++)
            {
                Console.Write($"\rYigin izleri getiriliyor... {i + 1}/{issues.Count}");
                try
                {
                    var r = await _service!.FetchReportsAsync(app, issues[i].Name, days, 3);
                    if (r.Count > 0) map[issues[i].Name] = r;
                }
                catch (GoogleApiException)
                {
                    // Tek bir konunun raporu alinamazsa digerleri devam etsin.
                    // Yarim ayrinti, hic ayrinti olmamasindan iyidir.
                }
            }

            Console.WriteLine();
            Console.WriteLine($"{map.Count}/{issues.Count} konu icin yigin izi alindi.");
            return map;
        }

        /// <summary>
        /// Gun sayisini kullanici belirliyor.
        ///
        /// Sabit bir varsayilan dayatmiyoruz cunku maliyet dogrudan buna
        /// bagli: 90 gun sorgulamak 7 gunden pahali ve cogu zaman gereksiz.
        /// </summary>
        private static int AskDays()
        {
            Console.Write("Kac gunluk? (Enter = 7): ");
            var raw = (Console.ReadLine() ?? "").Trim();
            if (raw.Length == 0) return 7;
            return int.TryParse(raw, out var d) && d > 0 ? d : 7;
        }

        private static void PrintRates(PlayVitalsSnapshot s)
        {
            Console.WriteLine();
            Console.WriteLine($"--- {s.DisplayName} [{s.PackageName}] ---");
            Console.WriteLine($"Aralik: {s.From:yyyy-MM-dd} .. {s.To:yyyy-MM-dd}  (Google saati: America/Los_Angeles)");
            Console.WriteLine();

            Console.WriteLine("ORAN                         SON GUN     ARALIK ORT.");
            Row("ANR", s.AnrRate, s.DistinctUsers);
            Row("ANR (kullanici algiladigi)", s.UserPerceivedAnrRate, s.DistinctUsers);
            Row("Crash", s.CrashRate, s.DistinctUsers);
            Row("Crash (kullanici algiladigi)", s.UserPerceivedCrashRate, s.DistinctUsers);

            var users = PlayVitalsSnapshot.Latest(s.DistinctUsers);
            Console.WriteLine();
            Console.WriteLine("Son gun benzersiz kullanici: " + (users.HasValue ? users.Value.ToString("N0") : "yok"));
        }

        private static void Row(string label, List<MetricPoint> series, List<MetricPoint> users)
        {
            var last = PlayVitalsSnapshot.Latest(series);
            var avg = PlayVitalsSnapshot.WeightedAverage(series, users);

            // Google oranlari 0..1 arasinda kesir olarak veriyor; yuzdeye
            // cevirmek okumayi kolaylastiriyor.
            string L = last.HasValue ? (last.Value * 100m).ToString("0.000") + "%" : "veri yok";
            string A = avg.HasValue ? (avg.Value * 100m).ToString("0.000") + "%" : "veri yok";

            Console.WriteLine($"{label,-28} {L,10}   {A,12}");
        }

        private static void PrintIssues(string header, List<ErrorIssueDto> issues)
        {
            Console.WriteLine();
            Console.WriteLine(header + " (" + issues.Count + ")");

            if (issues.Count == 0)
            {
                Console.WriteLine("  Kayit yok ya da bu hesap konu listesine erisemiyor.");
                return;
            }

            // Play Console'daki "Crashes and ANRs" tablosuyla ayni sutunlar:
            // etkilenen kullanici, olay sayisi, surum, son gorulme.
            Console.WriteLine($"  {"KULL.",6} {"OLAY",6}  {"SURUM",-12} {"SON GORULME",-17} KONU");
            Console.WriteLine("  " + new string('-', 74));

            foreach (var i in issues.Take(25))
            {
                string ver = i.LastAppVersion ?? "-";
                string seen = i.LastErrorReportTime ?? "-";
                string what = i.Cause ?? i.Type ?? "?";

                Console.WriteLine($"  {i.DistinctUsers,6} {i.ErrorReportCount,6}  {ver,-12} {seen,-17} {what}");

                if (!string.IsNullOrWhiteSpace(i.Location))
                    Console.WriteLine($"  {"",6} {"",6}  {"",-12} {"",-17} {i.Location}");

                if (!string.IsNullOrWhiteSpace(i.IssueUri))
                    Console.WriteLine($"  {"",6} {"",6}  {"",-12} {"",-17} Link: {i.IssueUri}");
            }

            if (issues.Count > 25) Console.WriteLine($"  ... ve {issues.Count - 25} tane daha");
        }

        /// <summary>
        /// Konulari bilgi bankasina yazar. AYNI KOD varsa uzerine yazmaz,
        /// yalnizca sayilari gunceller.
        ///
        /// Niye: kok neden ve cozum alanlarini insan dolduruyor. Yeniden
        /// cekim bunlari silerse bilgi bankasinin tek degerli kismi kaybolur.
        /// </summary>
        private async Task ImportAsync(
            PlayVitalsSnapshot snap,
            Dictionary<string, List<ErrorReportDto>>? reports = null)
        {
            var projects = (await _projectRepo.GetAllAsync()).ToList();
            var project = projects.FirstOrDefault(p =>
                string.Equals(p.PackageName, snap.PackageName, StringComparison.OrdinalIgnoreCase));

            if (project == null)
            {
                Console.WriteLine("Bu paket adiyla eslesen proje bilgi bankasinda yok: " + snap.PackageName);
                Console.WriteLine("Once projeyi ekle, sonra tekrar dene.");
                return;
            }

            var incoming = PlayVitalsSyncService.ToVitalIssues(snap, project.Id, project.Name ?? snap.DisplayName, reports);
            var existing = (await _issueRepo.GetAllAsync()).ToList();

            int added = 0, updated = 0;

            foreach (var issue in incoming)
            {
                var match = existing.FirstOrDefault(e =>
                    string.Equals(e.Code, issue.Code, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    await _issueRepo.AddAsync(issue);
                    added++;
                }
                else
                {
                    // Yalnizca olculen alanlar tazeleniyor. RootCause,
                    // FixApproach, LessonsLearned, Status ELLE yazilmis
                    // olabilir - onlara dokunmuyoruz.
                    match.EventCount = issue.EventCount;
                    match.AffectedUsers = issue.AffectedUsers;
                    match.ReportedVersion = issue.ReportedVersion ?? match.ReportedVersion;

                    // Yigin izi YALNIZCA bos ise doldurulur. Elle duzenlenmis
                    // ya da temizlenmis bir izi ezmek, insanin yaptigi isi
                    // silmek olurdu.
                    if (string.IsNullOrWhiteSpace(match.FullStackTrace)
                        && !string.IsNullOrWhiteSpace(issue.FullStackTrace))
                    {
                        match.FullStackTrace = issue.FullStackTrace;
                        foreach (var f in issue.SignatureFrames)
                            if (!match.SignatureFrames.Contains(f)) match.SignatureFrames.Add(f);
                        foreach (var d in issue.Devices)
                            if (!match.Devices.Any(x => x.Model == d.Model && x.AndroidVersion == d.AndroidVersion))
                                match.Devices.Add(d);
                    }
                    await _issueRepo.UpdateAsync(match);
                    updated++;
                }
            }

            Console.WriteLine($"Bilgi bankasi guncellendi: {added} yeni, {updated} tazelendi.");
        }

        private async Task SyncStoreFlowAsync()
        {
            if (_apps == null || _apps.Count == 0)
            {
                Console.WriteLine("Once uygulamalar getiriliyor...");
                _apps = await _service!.GetAppsAsync();
            }

            PlayAppDto? app = null;
            if (_apps.Count > 0)
            {
                Console.WriteLine();
                for (int i = 0; i < _apps.Count; i++)
                    Console.WriteLine($"{i + 1,3}) {_apps[i].DisplayName}  [{_apps[i].PackageName}]");

                Console.Write("Uygulama sec (varsayilan Blocked icin Enter): ");
                var raw = Console.ReadLine() ?? "";
                if (int.TryParse(raw, out int idx) && idx >= 1 && idx <= _apps.Count)
                    app = _apps[idx - 1];
                else
                    app = _apps.FirstOrDefault(a => a.PackageName.Contains("blocked", StringComparison.OrdinalIgnoreCase)) ?? _apps[0];
            }

            string pkg = app?.PackageName ?? "com.arargames.blocked";
            Console.WriteLine($"Secilen Paket: {pkg}");

            Console.Write("Referans varsayilan dil (Bos birakilirsa en-US): ");
            var defLang = (Console.ReadLine() ?? "").Trim();
            if (string.IsNullOrWhiteSpace(defLang)) defLang = "en-US";

            Console.WriteLine();
            Console.WriteLine($"[1/2] Magaza listelemeleri analiz ediliyor (Dry-run: {pkg}, Varsayilan: {defLang})...");
            var report = await _storeSyncService!.AnalyzeSyncAsync(pkg, defLang);

            Console.WriteLine();
            Console.WriteLine("=== ANALIZ VE KARSILASTIRMA RAPORU ===");
            Console.WriteLine($"Varsayilan Dil : {report.DefaultLanguage}");
            Console.WriteLine($"Hedef Video URL: {(string.IsNullOrWhiteSpace(report.TargetVideoUrl) ? "(Yok)" : report.TargetVideoUrl)}");
            Console.WriteLine();

            if (report.Diffs.Count == 0)
            {
                Console.WriteLine("Diger dillerde yerellestirilmis listeleme bulunamadi.");
                return;
            }

            Console.WriteLine($"{"Dil",-10} | {"YouTube Video Durumu",-35} | {"Ekran Goruntuleri"}");
            Console.WriteLine(new string('-', 85));

            int changesCount = 0;
            foreach (var d in report.Diffs)
            {
                string videoStatus = d.VideoWillUpdate
                    ? $"Guncellenecek (eski: {d.CurrentVideo ?? "Yok"})"
                    : "Ayni";

                int totalImg = d.PhoneScreenshotCount + d.SevenInchScreenshotCount + d.TenInchScreenshotCount;
                string imgStatus = d.ImagesWillClear
                    ? $"{totalImg} ozel gorsel temizlenecek (en-US devralinacak)"
                    : "Varsayilani kullaniyor";

                if (d.VideoWillUpdate || d.ImagesWillClear) changesCount++;

                Console.WriteLine($"{d.Language,-10} | {videoStatus,-35} | {imgStatus}");
            }
            Console.WriteLine(new string('-', 85));

            if (changesCount == 0)
            {
                Console.WriteLine("Tum diller zaten varsayilan magazayla birebir uyumlu! Degisiklik gerekmiyor.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"Toplam {changesCount} dilde degisiklik yapilacak.");
            Console.WriteLine("DIKKAT: Bu islem diger dillerdeki eski video linklerini hedef video ile guncelleyecek");
            Console.WriteLine("ve ozel ekran goruntulerini temizleyerek en-US gorsellerinin devralinmasini saglayacaktir.");
            Console.Write("Degisiklikler Google Play'e gonderilsin mi? (E/H): ");
            var confirm = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();

            if (confirm == "E")
            {
                Console.WriteLine("Google Play'e gonderiliyor (Commit)...");
                var result = await _storeSyncService.ExecuteSyncAsync(pkg, defLang);
                if (result.Committed)
                {
                    Console.WriteLine("BASARILI: Tum diller varsayilan magazayla esitlendi ve Google Play'e yayinlandi!");
                }
            }
            else
            {
                Console.WriteLine("Islem iptal edildi. Hicbir degisiklik yapilmadi.");
            }
        }

        private async Task ReauthorizeAsync()
        {
            Console.WriteLine("Mevcut oturum yenileniyor ve yeni izinlerle (Publisher) tarayici aciliyor...");
            await _tokens!.ReauthorizeAsync();
            Console.WriteLine("Giris islemi tamamlandi.");
        }

        private async Task LogoutAsync()
        {
            await _tokens!.RevokeAsync();
            _apps = null;
            Console.WriteLine("Google oturumu kapatildi ve saklanan jeton silindi.");
        }

        private static void PrintSetupHelp()
        {
            Console.WriteLine();
            Console.WriteLine("Google OAuth ayarlari bulunamadi.");
            Console.WriteLine();
            Console.WriteLine("Iki yoldan biriyle tanimla:");
            Console.WriteLine();
            Console.WriteLine("1) Ortam degiskeni:");
            Console.WriteLine("   setx HYDRA_GOOGLE_CLIENT_ID \"...\"");
            Console.WriteLine("   setx HYDRA_GOOGLE_CLIENT_SECRET \"...\"");
            Console.WriteLine();
            Console.WriteLine("2) Google Cloud Console'dan indirdigin istemci JSON'unu su yola koy:");
            Console.WriteLine("   " + PlayApiOptions.DefaultConfigPath);
            Console.WriteLine();
            Console.WriteLine("Adim adim kurulum: docs/PLAY_API_SETUP.md");
        }

        private static void Pause()
        {
            Console.WriteLine();
            Console.Write("Devam icin Enter...");
            Console.ReadLine();
        }
    }
}
