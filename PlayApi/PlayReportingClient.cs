using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra.Vitals.PlayApi
{
    /// <summary>
    /// Google API hatasi. Jeton ICERMEZ - istisna metni loga ve ekrana
    /// dustugu icin oraya kimlik bilgisi sizmasi kabul edilemez.
    /// </summary>
    public sealed class GoogleApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string? GoogleErrorStatus { get; }

        public GoogleApiException(HttpStatusCode status, string? googleStatus, string message)
            : base(message)
        {
            StatusCode = status;
            GoogleErrorStatus = googleStatus;
        }

        /// <summary>Kullaniciya gosterilecek, teknik olmayan aciklama.</summary>
        public string FriendlyMessage => StatusCode switch
        {
            HttpStatusCode.Unauthorized =>
                "Google oturumu gecersiz. Yeniden giris yapmayi dene (menu: Cikis yap).",
            HttpStatusCode.Forbidden =>
                "Google hesabinin bu islem icin gerekli Play Console izni yok. " +
                "Play Console > Kullanicilar ve izinler bolumunden hesaba en az " +
                "'Uygulama bilgilerini goruntule' yetkisi verilmeli, ayrica Google Cloud " +
                "projesinde Play Developer Reporting API etkin olmali.",
            HttpStatusCode.NotFound =>
                "Kaynak bulunamadi. Paket adi dogru mu ve uygulama bu hesapta erisilebilir mi?",
            HttpStatusCode.TooManyRequests =>
                "Google kota siniri asildi. Bir sure bekleyip tekrar dene.",
            _ => Message,
        };
    }

    /// <summary>
    /// Google API'lerine ortak HTTP davranisi.
    ///
    /// NEDEN AYRI SINIF: her uc nokta icin ayni yetkilendirme, ayni yeniden
    /// deneme ve ayni hata cozumleme kodunu tekrar yazmak, bir gun birinde
    /// duzeltip digerinde unutmak demektir.
    /// </summary>
    public abstract class GoogleApiClientBase
    {
        private readonly IGoogleTokenService _tokens;
        private readonly HttpClient _http;

        protected GoogleApiClientBase(IGoogleTokenService tokens, HttpClient http)
        {
            _tokens = tokens;
            _http = http;
        }

        protected async Task<JsonDocument> SendAsync(
            HttpMethod method, string url, string? jsonBody, CancellationToken ct)
        {
            // Tek bir yeniden deneme hakki:
            //   401 -> jeton yenile (bir kez)
            //   429/5xx -> bekleyip bir kez dene
            // Sonsuz dongu yok: Google kota asimini tekrar tekrar zorlamak
            // hesabi daha uzun sureli sinirlamalara sokar.
            for (int attempt = 0; ; attempt++)
            {
                using var req = new HttpRequestMessage(method, url);
                req.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer", await _tokens.GetAccessTokenAsync(ct));

                if (jsonBody != null)
                    req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                using var resp = await _http.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (resp.IsSuccessStatusCode)
                {
                    if (string.IsNullOrWhiteSpace(body)) return JsonDocument.Parse("{}");
                    return JsonDocument.Parse(body);
                }

                bool canRetry = attempt == 0 && (
                    resp.StatusCode == HttpStatusCode.Unauthorized ||
                    resp.StatusCode == HttpStatusCode.TooManyRequests ||
                    (int)resp.StatusCode >= 500);

                if (!canRetry)
                    throw Translate(resp.StatusCode, body);

                if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    // Google Retry-After verdiyse ona uy; vermediyse kisa bir
                    // sabit bekleme. Kendi kafamiza gore daha kisa beklemek
                    // sorunu buyutur.
                    var wait = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                    await Task.Delay(wait, ct);
                }
                else if ((int)resp.StatusCode >= 500)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), ct);
                }
                // 401'de bekleme yok: GetAccessTokenAsync zaten yenileyecek.
            }
        }

        private static GoogleApiException Translate(HttpStatusCode status, string body)
        {
            string? googleStatus = null;
            string message = "HTTP " + (int)status;

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var err))
                {
                    if (err.TryGetProperty("status", out var s)) googleStatus = s.GetString();
                    if (err.TryGetProperty("message", out var m)) message = m.GetString() ?? message;
                }
            }
            catch { /* govde JSON degil; durum kodu yeterli */ }

            return new GoogleApiException(status, googleStatus, message);
        }
    }

    // =========================================================================
    // DTO'lar
    // =========================================================================

    public sealed record PlayAppDto(string PackageName, string DisplayName, string AppResourceName);

    /// <summary>Bir gunun tek bir metrik satiri.</summary>
    public sealed record MetricPoint(DateOnly Date, decimal? Value);

    /// <summary>
    /// Bir hata konusu (crash ya da ANR kumesi).
    ///
    /// Alan adlari resmi ErrorIssue kaynagindan alindi: name, type, cause,
    /// location, errorReportCount, distinctUsers, lastErrorReportTime,
    /// firstAppVersion, lastAppVersion, firstOsVersion, lastOsVersion.
    /// Dokumanda olmayan alan UYDURULMADI.
    /// </summary>
    public sealed record ErrorIssueDto(
        string Name,
        string? Type,
        string? Cause,
        string? Location,
        long ErrorReportCount,
        long DistinctUsers,
        string? FirstAppVersion,
        string? LastAppVersion,
        string? LastErrorReportTime,
        string? IssueUri = null);

    /// <summary>
    /// TEK BIR HATA RAPORU — asil ayrinti burada.
    ///
    /// ErrorIssue yalnizca KUMENIN ozetidir (kac kullanici, hangi surum).
    /// Yigin izi (stack trace) issue'da YOKTUR; ErrorReport.reportText
    /// icindedir. Bilgi bankasinin degerli olmasi icin gereken sey de tam
    /// olarak bu metindir - bir dahaki sefere ayni imzayi taniyabilmek icin.
    ///
    /// Google'in kendi uyarisi: reportText makine tuketimi icin
    /// tasarlanmadi, bicimi zamanla degisebilir. Bu yuzden onu AYRISTIRMIYOR,
    /// oldugu gibi sakliyoruz; yalnizca ilk birkac satiri "imza" olarak
    /// ayirmak icin hafif bir tarama yapiliyor.
    /// </summary>
    public sealed record ErrorReportDto(
        string Name,
        string? Type,
        string? ReportText,
        string? IssueName,
        string? EventTime,
        string? DeviceBrand,
        string? DeviceModel,
        string? DeviceMarketingName,
        string? OsVersion,
        string? AppVersion);

    /// <summary>
    /// Play Developer Reporting API istemcisi.
    ///
    /// Uc noktalar 2026-09 itibariyla resmi dokumandan DOGRULANDI:
    ///   GET  /v1beta1/apps:search
    ///   POST /v1beta1/apps/{pkg}/anrRateMetricSet:query
    ///   POST /v1beta1/apps/{pkg}/crashRateMetricSet:query
    ///   GET  /v1beta1/apps/{pkg}/errorIssues:search
    ///
    /// v1beta1 su an Google'in referans dokumaninda birincil surum; v1alpha1
    /// de mevcut ama beta tercih edildi.
    /// </summary>
    public sealed class PlayReportingClient : GoogleApiClientBase
    {
        private const string Root = "https://playdeveloperreporting.googleapis.com/v1beta1";

        public PlayReportingClient(IGoogleTokenService tokens, HttpClient http) : base(tokens, http) { }

        /// <summary>Hesabin erisebildigi uygulamalar.</summary>
        public async Task<List<PlayAppDto>> SearchAppsAsync(CancellationToken ct = default)
        {
            var result = new List<PlayAppDto>();
            string? pageToken = null;

            do
            {
                var url = Root + "/apps:search?pageSize=1000";
                if (pageToken != null) url += "&pageToken=" + Uri.EscapeDataString(pageToken);

                using var doc = await SendAsync(HttpMethod.Get, url, null, ct);
                var root = doc.RootElement;

                if (root.TryGetProperty("apps", out var apps))
                {
                    foreach (var a in apps.EnumerateArray())
                    {
                        // Kaynak adi "apps/com.example.app" bicimindedir;
                        // paket adi son parcadir.
                        var name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var pkg = a.TryGetProperty("packageName", out var p)
                            ? p.GetString() ?? ""
                            : (name.Contains('/') ? name[(name.IndexOf('/') + 1)..] : name);
                        var display = a.TryGetProperty("displayName", out var d) ? d.GetString() ?? pkg : pkg;

                        result.Add(new PlayAppDto(pkg, display, name));
                    }
                }

                pageToken = root.TryGetProperty("nextPageToken", out var t) ? t.GetString() : null;
            }
            while (!string.IsNullOrEmpty(pageToken));

            return result;
        }

        /// <summary>
        /// Metrik setinin TAZELIK sinirini okur: Google'in o set icin
        /// hazirladigi EN SON gun.
        ///
        /// NIYE ZORUNLU — bu hata bir kez alindi:
        ///     'timeline_spec.end_date' field should be at most the current
        ///     freshness 2026-09-02 00:00
        ///
        /// Yani bitis gunu tazelikten ileride olamaz. "Dun" demek yetmiyor;
        /// Google bazen iki gun geriden geliyor ve gecikme metrik setine gore
        /// degisiyor. Tahmin etmek yerine SORUYORUZ.
        ///
        /// GET /v1beta1/apps/{pkg}/{metricSet}
        /// -> freshnessInfo.freshness[] icinde aggregationPeriod=DAILY olanin
        ///    latestEndTime alani.
        /// </summary>
        public async Task<DateOnly?> GetFreshnessAsync(
            string packageName, string metricSet, CancellationToken ct = default)
        {
            var url = $"{Root}/apps/{Uri.EscapeDataString(packageName)}/{metricSet}";

            using var doc = await SendAsync(HttpMethod.Get, url, null, ct);
            if (!doc.RootElement.TryGetProperty("freshnessInfo", out var fi)) return null;
            if (!fi.TryGetProperty("freshness", out var arr)) return null;

            foreach (var f in arr.EnumerateArray())
            {
                var period = f.TryGetProperty("aggregationPeriod", out var ap) ? ap.GetString() : null;
                if (period != null && period != "DAILY") continue;

                if (f.TryGetProperty("latestEndTime", out var let))
                {
                    var d = ReadDate(let);
                    if (d != default) return d;
                }
            }

            return null;
        }

        public Task<Dictionary<string, List<MetricPoint>>> QueryAnrRateAsync(
            string packageName, DateOnly from, DateOnly to, CancellationToken ct = default)
            => QueryMetricSetAsync(packageName, "anrRateMetricSet",
                new[] { "anrRate", "userPerceivedAnrRate", "distinctUsers" }, from, to, ct);

        public Task<Dictionary<string, List<MetricPoint>>> QueryCrashRateAsync(
            string packageName, DateOnly from, DateOnly to, CancellationToken ct = default)
            => QueryMetricSetAsync(packageName, "crashRateMetricSet",
                new[] { "crashRate", "userPerceivedCrashRate", "distinctUsers" }, from, to, ct);

        /// <summary>
        /// Ortak metrik seti sorgusu.
        ///
        /// GUNLUK toplama kullaniliyor ve saat dilimi America/Los_Angeles.
        /// Bu bizim tercihimiz DEGIL: dokuman "tarihsel kisitlar nedeniyle
        /// DAILY icin desteklenen tek saat dilimi budur" diyor. Baska bir
        /// deger gondermek hata dondurur.
        ///
        /// Boyutlara (dimensions) BOLUNMUYOR: tek bir gunluk toplam
        /// isteniyorsa bolme yapmamak dogru olan. Cihaza/surume gore kirilim
        /// ileride ayri bir sorgu olarak eklenir.
        /// </summary>
        private async Task<Dictionary<string, List<MetricPoint>>> QueryMetricSetAsync(
            string packageName, string metricSet, string[] metrics,
            DateOnly from, DateOnly to, CancellationToken ct)
        {
            // TAZELIGE KIRP. Bitis gunu Google'in hazirladigi son gunden
            // ileride olamaz - aksi halde istek 400 doner ve hicbir veri
            // gelmez. Tazelik alinamazsa istegi oldugu gibi gondeririz;
            // sessizce baska bir tarihe kaymaktansa Google'in hatasini
            // gormek daha dogru.
            var freshness = await GetFreshnessAsync(packageName, metricSet, ct);
            if (freshness is DateOnly f && to > f)
            {
                to = f;
                if (from > to) from = to;
            }

            var url = $"{Root}/apps/{Uri.EscapeDataString(packageName)}/{metricSet}:query";

            var body = new StringBuilder();
            body.Append("{\"timelineSpec\":{\"aggregationPeriod\":\"DAILY\",");
            body.Append("\"startTime\":").Append(DatePart(from)).Append(',');
            body.Append("\"endTime\":").Append(DatePart(to)).Append("},");
            body.Append("\"metrics\":[");
            for (int i = 0; i < metrics.Length; i++)
            {
                if (i > 0) body.Append(',');
                body.Append('"').Append(metrics[i]).Append('"');
            }
            body.Append("],\"pageSize\":1000}");

            var series = new Dictionary<string, List<MetricPoint>>(StringComparer.Ordinal);
            foreach (var m in metrics) series[m] = new List<MetricPoint>();

            using var doc = await SendAsync(HttpMethod.Post, url, body.ToString(), ct);
            if (!doc.RootElement.TryGetProperty("rows", out var rows)) return series;

            foreach (var row in rows.EnumerateArray())
            {
                DateOnly date = default;
                if (row.TryGetProperty("startTime", out var st)) date = ReadDate(st);

                if (!row.TryGetProperty("metrics", out var ms)) continue;

                foreach (var metric in ms.EnumerateArray())
                {
                    var key = metric.TryGetProperty("metric", out var k) ? k.GetString() : null;
                    if (key == null || !series.ContainsKey(key)) continue;

                    // Google decimal degerleri STRING olarak dondurur
                    // (google.type.Decimal). double'a cevirmek sessiz hassasiyet
                    // kaybi demek; decimal.Parse ile okuyoruz.
                    decimal? value = null;
                    if (metric.TryGetProperty("decimalValue", out var dv)
                        && dv.TryGetProperty("value", out var raw))
                    {
                        var s = raw.GetString();
                        if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                            value = d;
                    }

                    series[key].Add(new MetricPoint(date, value));
                }
            }

            foreach (var list in series.Values) list.Sort((a, b) => a.Date.CompareTo(b.Date));
            return series;
        }

        /// <summary>
        /// Hata konulari (crash / ANR kumeleri).
        ///
        /// Filtre AIP-160 bicimindedir; errorIssueType ile ANR ve CRASH
        /// ayristirilir.
        ///
        /// DIKKAT — BU METOT EN AZ DOGRULANMIS KISIM.
        /// Kaynak ve alan adlari (name, type, cause, location,
        /// errorReportCount, distinctUsers, lastErrorReportTime,
        /// firstAppVersion, lastAppVersion) resmi ErrorIssue dokumanindan
        /// alindi. Ancak ic ice gecmis 'interval' mesajinin SORGU DIZESINE
        /// nasil duzlestirildigi (interval.startTime.year gibi) gRPC
        /// transcoding'in genel kuralindan turetildi, ornek istekle
        /// dogrulanmadi.
        ///
        /// Bu yuzden cagiran taraf (PlayVitalsSyncService.SafeIssuesAsync)
        /// hatayi yutup bos liste donuyor: konu listesi alinamasa bile ORAN
        /// verisi gecerli ve degerli. Ilk gercek calistirmada burasi 400
        /// donerse duzeltilecek yer sorgu dizesidir.
        /// </summary>
        public async Task<List<ErrorIssueDto>> SearchErrorIssuesAsync(
            string packageName, string errorIssueType, DateOnly from, DateOnly to,
            int maxResults = 50, CancellationToken ct = default)
        {
            var url = $"{Root}/apps/{Uri.EscapeDataString(packageName)}/errorIssues:search"
                    + "?pageSize=" + Math.Clamp(maxResults, 1, 1000)
                    + "&filter=" + Uri.EscapeDataString($"errorIssueType = {errorIssueType}")
                    + Interval(from, to)
                    + "&orderBy=" + Uri.EscapeDataString("errorReportCount desc");

            var list = new List<ErrorIssueDto>();

            using var doc = await SendAsync(HttpMethod.Get, url, null, ct);
            if (!doc.RootElement.TryGetProperty("errorIssues", out var issues)) return list;

            foreach (var i in issues.EnumerateArray())
            {
                list.Add(new ErrorIssueDto(
                    Name: Str(i, "name") ?? "",
                    Type: Str(i, "type"),
                    Cause: Str(i, "cause"),
                    Location: Str(i, "location"),
                    ErrorReportCount: Long(i, "errorReportCount"),
                    DistinctUsers: Long(i, "distinctUsers"),
                    FirstAppVersion: AppVersion(i, "firstAppVersion"),
                    LastAppVersion: AppVersion(i, "lastAppVersion"),
                    LastErrorReportTime: DateTimeText(i, "lastErrorReportTime"),
                    IssueUri: Str(i, "issueUri")));
            }

            return list;
        }

        /// <summary>
        /// Bir KONUYA ait hata raporlari — yigin izleri dahil.
        ///
        /// GET /v1beta1/apps/{pkg}/errorReports:search
        ///
        /// issueName "apps/{pkg}/errorIssues/{id}" tam kaynak adidir; filtre
        /// bu adla eslesiyor. Bos verilirse tum raporlar doner (nadiren
        /// istenen sey - genelde tek bir konunun izini goruyoruz).
        /// </summary>
        public async Task<List<ErrorReportDto>> SearchErrorReportsAsync(
            string packageName, string? issueName, DateOnly from, DateOnly to,
            int maxResults = 5, CancellationToken ct = default)
        {
            var url = $"{Root}/apps/{Uri.EscapeDataString(packageName)}/errorReports:search"
                    + "?pageSize=" + Math.Clamp(maxResults, 1, 100)
                    + Interval(from, to);

            if (!string.IsNullOrWhiteSpace(issueName))
                url += "&filter=" + Uri.EscapeDataString($"issue = \"{issueName}\"");

            var list = new List<ErrorReportDto>();

            using var doc = await SendAsync(HttpMethod.Get, url, null, ct);
            if (!doc.RootElement.TryGetProperty("errorReports", out var reports)) return list;

            foreach (var r in reports.EnumerateArray())
            {
                string? brand = null, model = null, marketing = null;
                if (r.TryGetProperty("deviceModel", out var dm))
                {
                    marketing = Str(dm, "marketingName");
                    if (dm.TryGetProperty("deviceId", out var did))
                    {
                        brand = Str(did, "buildBrand");
                        model = Str(did, "buildDevice");
                    }
                }

                string? os = null;
                if (r.TryGetProperty("osVersion", out var ov))
                    os = ov.TryGetProperty("apiLevel", out var al) ? al.ToString() : null;

                list.Add(new ErrorReportDto(
                    Name: Str(r, "name") ?? "",
                    Type: Str(r, "type"),
                    ReportText: Str(r, "reportText"),
                    IssueName: Str(r, "issue"),
                    EventTime: Str(r, "eventTime"),
                    DeviceBrand: brand,
                    DeviceModel: model,
                    DeviceMarketingName: marketing,
                    OsVersion: os,
                    AppVersion: AppVersion(r, "appVersion")));
            }

            return list;
        }

        /// <summary>
        /// Ortak zaman araligi sorgu dizesi — errorIssues ve errorReports icin.
        ///
        /// IKI DETAY DENEYEREK OGRENILDI, dokumandan degil:
        ///
        ///   1. SAAT DILIMI GONDERILMIYOR. Bu uc noktalar, metrik seti
        ///      sorgularinin aksine, saat dilimi verilmedigi zaman UTC
        ///      varsayiyor. America/Los_Angeles gondermek burada calismiyor -
        ///      metrik setlerinde ise ZORUNLU. Ikisi ayni API'nin parcasi ama
        ///      ayni kurala uymuyor.
        ///
        ///   2. BITIS DISLAYICI (exclusive). 'to' gununu de kapsamak icin bir
        ///      gun ekleniyor; eklenmezse son gunun raporlari kayboluyor.
        ///
        /// Tek yerde tutuluyor ki iki cagri zamanla birbirinden ayrismasin.
        /// </summary>
        private static string Interval(DateOnly from, DateOnly to)
        {
            var end = to.AddDays(1);
            return "&interval.startTime.year=" + from.Year
                 + "&interval.startTime.month=" + from.Month
                 + "&interval.startTime.day=" + from.Day
                 + "&interval.endTime.year=" + end.Year
                 + "&interval.endTime.month=" + end.Month
                 + "&interval.endTime.day=" + end.Day;
        }

        // --- kucuk okuma yardimcilari ---

        private static string? Str(JsonElement e, string name)
            => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static long Long(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var v)) return 0;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)) return n;
            if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out var s)) return s;
            return 0;
        }

        /// <summary>AppVersion nesnesi { "versionCode": "142" } bicimindedir.</summary>
        private static string? AppVersion(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Object) return null;
            if (!v.TryGetProperty("versionCode", out var vc)) return null;
            return vc.ValueKind == JsonValueKind.String ? vc.GetString() : vc.ToString();
        }

        /// <summary>
        /// Tarih-saat bilgisini (ISO-8601 string veya google.type.DateTime nesnesi)
        /// okunabilir metne cevirir.
        /// </summary>
        private static string? DateTimeText(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var v)) return null;

            if (v.ValueKind == JsonValueKind.String)
            {
                var s = v.GetString();
                if (string.IsNullOrEmpty(s)) return null;
                if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
                    return dto.ToString("yyyy-MM-dd HH:mm");
                return s;
            }

            if (v.ValueKind == JsonValueKind.Object)
            {
                int Get(string f) => v.TryGetProperty(f, out var x) && x.TryGetInt32(out var n) ? n : 0;
                int y = Get("year"), mo = Get("month"), d = Get("day");
                if (y == 0) return null;
                int h = Get("hours"), mi = Get("minutes");
                return $"{y:0000}-{mo:00}-{d:00} {h:00}:{mi:00}";
            }

            return null;
        }

        /// <summary>
        /// google.type.DateTime parcasi. Saat dilimi America/Los_Angeles -
        /// gunluk toplamada API'nin kabul ettigi tek deger.
        /// </summary>
        private static string DatePart(DateOnly d)
            => $"{{\"year\":{d.Year},\"month\":{d.Month},\"day\":{d.Day},\"timeZone\":{{\"id\":\"America/Los_Angeles\"}}}}";

        private static DateOnly ReadDate(JsonElement e)
        {
            int y = e.TryGetProperty("year", out var a) ? a.GetInt32() : 1;
            int m = e.TryGetProperty("month", out var b) ? b.GetInt32() : 1;
            int d = e.TryGetProperty("day", out var c) ? c.GetInt32() : 1;
            try { return new DateOnly(y, m, d); } catch { return default; }
        }
    }
}
