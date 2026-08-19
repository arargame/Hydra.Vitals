using System;
using System.IO;
using System.Threading.Tasks;
using Hydra.Vitals.Data;
using Hydra.Vitals.Services;
using Hydra.Vitals.UI;

namespace Hydra.Vitals
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            try { Console.Title = "Hydra.Vitals - Unified AI & Mobile Diagnostics Knowledge Base"; } catch { }

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
            var dashboard = new ConsoleDashboard(analysisService);
            await dashboard.RunAsync();
        }
    }
}