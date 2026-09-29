using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra.Vitals.PlayApi
{
    /// <summary>
    /// Google OAuth ayarlari.
    ///
    /// SIR YONETIMI: ClientId/ClientSecret KAYNAK KONTROLUNE GIRMEZ. Bu araç
    /// once ortam degiskenlerine, sonra kullanici profilindeki bir yerel
    /// dosyaya bakar. Ikisi de yoksa kurulum yonergesini yazip cikar.
    ///
    /// Neden appsettings.json degil: bu bir konsol araci ve depo herkese acik
    /// olabilir. "Sonra .gitignore'a ekleriz" en sik sizinti sebebidir.
    /// </summary>
    public sealed class PlayApiOptions
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>Yenileme jetonunun saklandigi dosya.</summary>
        public string TokenStorePath { get; set; } = string.Empty;

        public const string ReportingScope = "https://www.googleapis.com/auth/playdeveloperreporting";
        public const string PublisherScope = "https://www.googleapis.com/auth/androidpublisher";
        public const string AllScopes = "https://www.googleapis.com/auth/playdeveloperreporting https://www.googleapis.com/auth/androidpublisher";

        public static string DefaultConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HydraVitals", "google_oauth.json");

        public static string DefaultTokenPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HydraVitals", "google_token.bin");

        /// <summary>
        /// Once ortam degiskeni, sonra yerel yapilandirma dosyasi.
        /// Bulunamazsa null doner - cagiran taraf kurulum metnini gosterir.
        /// </summary>
        public static PlayApiOptions? Load()
        {
            var id = Environment.GetEnvironmentVariable("HYDRA_GOOGLE_CLIENT_ID");
            var secret = Environment.GetEnvironmentVariable("HYDRA_GOOGLE_CLIENT_SECRET");

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret))
            {
                if (!File.Exists(DefaultConfigPath)) return null;
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(DefaultConfigPath));
                    var root = doc.RootElement;

                    // Google Cloud Console'dan indirilen istemci JSON'u
                    // "installed" ya da "web" altinda tasir; ikisini de kabul et.
                    if (root.TryGetProperty("installed", out var inst)) root = inst;
                    else if (root.TryGetProperty("web", out var web)) root = web;

                    id = root.TryGetProperty("client_id", out var a) ? a.GetString() : null;
                    secret = root.TryGetProperty("client_secret", out var b) ? b.GetString() : null;
                }
                catch { return null; }
            }

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret)) return null;

            return new PlayApiOptions
            {
                ClientId = id!,
                ClientSecret = secret!,
                TokenStorePath = DefaultTokenPath,
            };
        }
    }

    /// <summary>
    /// Yenileme jetonunu diskte korur.
    ///
    /// Yenileme jetonu SURESIZ bir anahtardir: eline gecen kisi senin adina
    /// Play verisine erisir. Duz metin yazmak, parolayi masaustune yazmakla
    /// aynidir.
    /// </summary>
    public interface ITokenProtector
    {
        byte[] Protect(string plaintext);
        string? Unprotect(byte[] payload);
    }

    /// <summary>
    /// Windows DPAPI. Anahtar kullanici hesabina baglidir; baska bir kullanici
    /// ya da baska bir makine dosyayi cozemez.
    /// </summary>
    public sealed class DpapiTokenProtector : ITokenProtector
    {
        public static bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public byte[] Protect(string plaintext)
            => ProtectedData.Protect(Encoding.UTF8.GetBytes(plaintext), null, DataProtectionScope.CurrentUser);

        public string? Unprotect(byte[] payload)
        {
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(payload, null, DataProtectionScope.CurrentUser)); }
            catch { return null; }   // baska kullanici/makine, ya da bozuk dosya
        }
    }

    /// <summary>
    /// DPAPI'nin olmadigi platformlarda yedek: jeton SAKLANMAZ.
    ///
    /// Bilerek boyle: duz metin yazmaktansa her calistirmada yeniden giris
    /// yapmak daha iyidir. "Gecici olarak duz yazalim" diye baslayan sey
    /// kalicilasir.
    /// </summary>
    public sealed class NoPersistTokenProtector : ITokenProtector
    {
        public byte[] Protect(string plaintext) => Array.Empty<byte>();
        public string? Unprotect(byte[] payload) => null;
    }

    public interface IGoogleTokenService
    {
        Task<string> GetAccessTokenAsync(CancellationToken ct = default);
        Task<string> ReauthorizeAsync(CancellationToken ct = default);
        Task<bool> HasValidCredentialsAsync(CancellationToken ct = default);
        Task RevokeAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Masaustu/konsol uygulamalari icin OAuth 2.0 — LOOPBACK akisi.
    ///
    /// NEDEN LOOPBACK: konsol uygulamasinda gizli tutulabilen bir sunucu yok.
    /// Google'in yuklu uygulamalar icin onerdigi yol, tarayiciyi acip
    /// 127.0.0.1 uzerindeki gecici bir dinleyiciye geri donmektir. "Out-of-band"
    /// (kopyala-yapistir kod) akisi Google tarafindan kullanimdan kaldirildi.
    ///
    /// PKCE kullaniliyor: yetkilendirme kodu ele gecse bile dogrulayici
    /// olmadan jetona cevrilemez.
    ///
    /// access_type=offline + prompt=consent: yenileme jetonu ancak boyle
    /// geliyor. Google yenileme jetonunu yalnizca ILK onayda dondurur; bu
    /// yuzden onay ekrani acikca isteniyor.
    /// </summary>
    public sealed class GoogleTokenService : IGoogleTokenService
    {
        private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
        private const string RevokeEndpoint = "https://oauth2.googleapis.com/revoke";

        private readonly PlayApiOptions _options;
        private readonly ITokenProtector _protector;
        private readonly HttpClient _http;

        private string? _accessToken;
        private DateTimeOffset _accessExpiresAt = DateTimeOffset.MinValue;
        private string? _refreshToken;

        public GoogleTokenService(PlayApiOptions options, ITokenProtector protector, HttpClient http)
        {
            _options = options;
            _protector = protector;
            _http = http;
            _refreshToken = LoadRefreshToken();
        }

        public Task<bool> HasValidCredentialsAsync(CancellationToken ct = default)
            => Task.FromResult(!string.IsNullOrEmpty(_refreshToken));

        public async Task<string> ReauthorizeAsync(CancellationToken ct = default)
        {
            _refreshToken = null;
            _accessToken = null;
            _accessExpiresAt = DateTimeOffset.MinValue;
            DeleteRefreshToken();
            await AuthorizeInteractivelyAsync(ct);
            return _accessToken!;
        }

        public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
        {
            // 60 sn emniyet payi: tam sinirda gonderilen bir istek yolda
            // suresi dolmus jetona donusebilir.
            if (_accessToken != null && DateTimeOffset.UtcNow < _accessExpiresAt.AddSeconds(-60))
                return _accessToken;

            if (!string.IsNullOrEmpty(_refreshToken))
            {
                var refreshed = await TryRefreshAsync(ct);
                if (refreshed) return _accessToken!;

                // Yenileme jetonu iptal edilmis ya da suresi dolmus.
                _refreshToken = null;
                DeleteRefreshToken();
            }

            await AuthorizeInteractivelyAsync(ct);
            return _accessToken!;
        }

        private async Task<bool> TryRefreshAsync(CancellationToken ct)
        {
            var form = new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["refresh_token"] = _refreshToken!,
                ["grant_type"] = "refresh_token",
            };

            using var resp = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form), ct);
            if (!resp.IsSuccessStatusCode) return false;

            var json = await resp.Content.ReadAsStringAsync(ct);
            ApplyTokenResponse(json);
            return _accessToken != null;
        }

        private async Task AuthorizeInteractivelyAsync(CancellationToken ct)
        {
            // Port 0: isletim sistemi bos bir port secsin. Sabit port secmek
            // "adres zaten kullanimda" hatasina davetiye cikarir.
            var listener = new HttpListener();
            int port = GetFreePort();
            string redirectUri = $"http://127.0.0.1:{port}/";
            listener.Prefixes.Add(redirectUri);
            listener.Start();

            string verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
            string challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            string state = Base64Url(RandomNumberGenerator.GetBytes(16));

            var url = AuthEndpoint
                + "?client_id=" + Uri.EscapeDataString(_options.ClientId)
                + "&redirect_uri=" + Uri.EscapeDataString(redirectUri)
                + "&response_type=code"
                + "&scope=" + Uri.EscapeDataString(PlayApiOptions.AllScopes)
                + "&code_challenge=" + challenge
                + "&code_challenge_method=S256"
                + "&state=" + state
                + "&access_type=offline"
                + "&prompt=consent";

            Console.WriteLine();
            Console.WriteLine("Tarayici aciliyor. Google hesabinla giris yap ve izin ver.");
            Console.WriteLine("Acilmazsa su adresi elle ac:");
            Console.WriteLine(url);
            Console.WriteLine();

            TryOpenBrowser(url);

            var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromMinutes(5), ct);
            var query = context.Request.QueryString;

            string body;
            string? code = query["code"];
            string? returnedState = query["state"];
            string? error = query["error"];

            if (error != null) body = "Yetkilendirme reddedildi: " + WebUtility.HtmlEncode(error);
            else if (returnedState != state) body = "Guvenlik hatasi: state eslesmedi.";
            else if (code == null) body = "Yetkilendirme kodu gelmedi.";
            else body = "Giris tamam. Bu sekmeyi kapatabilirsin.";

            var buffer = Encoding.UTF8.GetBytes(
                "<html><head><meta charset=\"utf-8\"><title>Hydra.Vitals</title></head>" +
                "<body style=\"font-family:system-ui;background:#0d1117;color:#c9d1d9;padding:40px\">" +
                "<h2>Hydra.Vitals</h2><p>" + body + "</p></body></html>");
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, ct);
            context.Response.Close();
            listener.Stop();

            if (error != null) throw new InvalidOperationException("Google yetkilendirme reddedildi: " + error);
            if (returnedState != state) throw new InvalidOperationException("OAuth state eslesmedi (olasi CSRF).");
            if (code == null) throw new InvalidOperationException("Yetkilendirme kodu alinamadi.");

            var form = new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["code"] = code,
                ["code_verifier"] = verifier,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri,
            };

            using var resp = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form), ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                // Govde jeton icermiyor (hata cevabi) ama yine de temkinli
                // olup yalnizca durum kodunu ve hata alanini gosteriyoruz.
                throw new InvalidOperationException(
                    "Jeton alinamadi (HTTP " + (int)resp.StatusCode + "). " + ExtractError(json));
            }

            ApplyTokenResponse(json);
            if (_refreshToken != null) SaveRefreshToken(_refreshToken);
        }

        private void ApplyTokenResponse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("access_token", out var at)) _accessToken = at.GetString();
            if (root.TryGetProperty("expires_in", out var ei))
                _accessExpiresAt = DateTimeOffset.UtcNow.AddSeconds(ei.GetInt32());

            // Yenileme jetonu YALNIZCA ilk onayda gelir; sonraki yenileme
            // cevaplarinda alan yoktur. Var olani ezmemek kritik.
            if (root.TryGetProperty("refresh_token", out var rt))
            {
                var v = rt.GetString();
                if (!string.IsNullOrEmpty(v)) _refreshToken = v;
            }
        }

        private static string ExtractError(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error_description", out var d)) return d.GetString() ?? "";
                if (doc.RootElement.TryGetProperty("error", out var e)) return e.GetString() ?? "";
            }
            catch { }
            return "";
        }

        public async Task RevokeAsync(CancellationToken ct = default)
        {
            if (!string.IsNullOrEmpty(_refreshToken))
            {
                var form = new Dictionary<string, string> { ["token"] = _refreshToken! };
                try { await _http.PostAsync(RevokeEndpoint, new FormUrlEncodedContent(form), ct); } catch { }
            }
            _refreshToken = null;
            _accessToken = null;
            _accessExpiresAt = DateTimeOffset.MinValue;
            DeleteRefreshToken();
        }

        // --- disk ---

        private string? LoadRefreshToken()
        {
            try
            {
                if (!File.Exists(_options.TokenStorePath)) return null;
                return _protector.Unprotect(File.ReadAllBytes(_options.TokenStorePath));
            }
            catch { return null; }
        }

        private void SaveRefreshToken(string token)
        {
            try
            {
                var payload = _protector.Protect(token);
                if (payload.Length == 0) return;   // NoPersist: bilerek saklamiyoruz

                Directory.CreateDirectory(Path.GetDirectoryName(_options.TokenStorePath)!);
                File.WriteAllBytes(_options.TokenStorePath, payload);
            }
            catch { /* saklanamadi: bir dahaki sefere yeniden giris istenir */ }
        }

        private void DeleteRefreshToken()
        {
            try { if (File.Exists(_options.TokenStorePath)) File.Delete(_options.TokenStorePath); } catch { }
        }

        // --- yardimcilar ---

        private static string Base64Url(byte[] data)
            => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static int GetFreePort()
        {
            var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        private static void TryOpenBrowser(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true,
                });
            }
            catch { /* kullanici adresi elle acar */ }
        }
    }
}
