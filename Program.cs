using System;
using System.IO;
using System.Threading.Tasks;
using Hydra.Vitals.Data;
using Hydra.Vitals.Services;
using Hydra.Vitals.PlayApi;
using Hydra.Vitals.UI;

namespace Hydra.Vitals
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            try { Console.Title = "Hydra.Vitals - Unified AI & Mobile Diagnostics Knowledge Base"; } catch { }

            if (args.Length > 0)
            {
                await HandleCliArgsAsync(args);
                return;
            }

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var projectDir = Path.GetFullPath(Path.Combine(appDir, "..", "..", ".."));
            var dbPath = File.Exists(Path.Combine(projectDir, "vitals_database.json"))
                ? Path.Combine(projectDir, "vitals_database.json")
                : Path.Combine(appDir, "vitals_database.json");

            var dbContext = new JsonDatabaseContext(dbPath);

            // 1. Veri Tohumlama (Seed)
            await VitalDataSeeder.SeedAsync(dbContext);

            // 2. Repository & Service Katmani (IoC / Dependency Injection)
            var issueRepo = new VitalIssueJsonRepository(dbContext);
            var projectRepo = new ProjectJsonRepository(dbContext);
            var analysisService = new VitalAnalysisService(issueRepo, projectRepo);

            // 3. UI Dashboard Calistirma
            //
            // Iki ekran var ve bilerek ayrilar:
            //   ConsoleDashboard -> elle girilmis bilgi bankasi (kurum hafizasi)
            //   PlayVitalsMenu   -> Google Play'den CANLI crash/ANR verisi
            var playMenu = new PlayVitalsMenu(issueRepo, projectRepo);
            var dashboard = new ConsoleDashboard(analysisService, playMenu);
            await dashboard.RunAsync();
        }

        private static async Task HandleCliArgsAsync(string[] args)
        {
            var options = PlayApiOptions.Load();
            if (options == null)
            {
                Console.WriteLine("Google OAuth configuration not found. Check PLAY_API_SETUP.md.");
                return;
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            ITokenProtector protector = DpapiTokenProtector.IsSupported
                ? new DpapiTokenProtector()
                : new NoPersistTokenProtector();

            var tokens = new GoogleTokenService(options, protector, http);

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i].ToLowerInvariant();
                if (arg == "--list-apps")
                {
                    var client = new PlayReportingClient(tokens, http);
                    Console.WriteLine("Fetching accessible applications...");
                    var apps = await client.SearchAppsAsync();
                    foreach (var a in apps)
                    {
                        Console.WriteLine($"App: {a.DisplayName} [{a.PackageName}]");
                    }
                    return;
                }
                else if (arg == "--store-listing" && i + 1 < args.Length)
                {
                    string pkg = args[i + 1];
                    string? lang = null;
                    if (i + 3 < args.Length && args[i + 2].ToLowerInvariant() == "--lang")
                    {
                        lang = args[i + 3];
                    }

                    var pubClient = new PlayPublisherClient(tokens, http);
                    Console.WriteLine($"Opening edit for package: {pkg}...");
                    string editId = await pubClient.InsertEditAsync(pkg);
                    try
                    {
                        var listings = await pubClient.GetListingsAsync(pkg, editId);
                        Console.WriteLine($"Found {listings.Count} store listings.");

                        var targetLang = lang;
                        if (string.IsNullOrWhiteSpace(targetLang))
                        {
                            var def = listings.FirstOrDefault(l => l.Language.Equals("en-US", StringComparison.OrdinalIgnoreCase))
                                ?? listings.FirstOrDefault(l => l.Language.Equals("en-GB", StringComparison.OrdinalIgnoreCase))
                                ?? listings.FirstOrDefault(l => l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                                ?? listings.FirstOrDefault();

                            targetLang = def?.Language ?? "en-US";
                        }

                        Console.WriteLine($"Fetching details for language: {targetLang}...");
                        var details = await pubClient.GetFullListingDetailsAsync(pkg, editId, targetLang);

                        if (details == null)
                        {
                            Console.WriteLine($"No listing found for language '{targetLang}'. Available languages: {string.Join(", ", listings.Select(l => l.Language))}");
                        }
                        else
                        {
                            Console.WriteLine("==================================================");
                            Console.WriteLine($"STORE LISTING: {pkg} [{details.Language}]");
                            Console.WriteLine("==================================================");
                            Console.WriteLine($"TITLE: {details.Title}");
                            Console.WriteLine("--------------------------------------------------");
                            Console.WriteLine($"SHORT DESCRIPTION:\n{details.ShortDescription}");
                            Console.WriteLine("--------------------------------------------------");
                            Console.WriteLine($"FULL DESCRIPTION:\n{details.FullDescription}");
                            Console.WriteLine("--------------------------------------------------");
                            Console.WriteLine($"VIDEO URL: {details.Video ?? "(None)"}");
                            Console.WriteLine("--------------------------------------------------");
                            Console.WriteLine($"ICONS ({details.Icons.Count}):");
                            foreach (var img in details.Icons) Console.WriteLine($"  ID: {img.Id} | URL: {img.Url}");
                            Console.WriteLine($"FEATURE GRAPHICS ({details.FeatureGraphics.Count}):");
                            foreach (var img in details.FeatureGraphics) Console.WriteLine($"  ID: {img.Id} | URL: {img.Url}");
                            Console.WriteLine($"PHONE SCREENSHOTS ({details.PhoneScreenshots.Count}):");
                            foreach (var img in details.PhoneScreenshots) Console.WriteLine($"  ID: {img.Id} | URL: {img.Url}");
                            Console.WriteLine($"7-INCH SCREENSHOTS ({details.SevenInchScreenshots.Count}):");
                            foreach (var img in details.SevenInchScreenshots) Console.WriteLine($"  ID: {img.Id} | URL: {img.Url}");
                            Console.WriteLine($"10-INCH SCREENSHOTS ({details.TenInchScreenshots.Count}):");
                            foreach (var img in details.TenInchScreenshots) Console.WriteLine($"  ID: {img.Id} | URL: {img.Url}");
                            Console.WriteLine("==================================================");
                        }
                    }
                    finally
                    {
                        await pubClient.DeleteEditAsync(pkg, editId);
                    }
                    return;
                }
                else if (arg == "--reauth")
                {
                    Console.WriteLine("Reauthorizing Google credentials...");
                    await tokens.ReauthorizeAsync();
                    Console.WriteLine("Reauthorization complete.");
                    return;
                }
            }

            Console.WriteLine("Usage:");
            Console.WriteLine("  --list-apps");
            Console.WriteLine("  --store-listing <package_name> [--lang <language_code>]");
            Console.WriteLine("  --reauth");
        }
    }
}