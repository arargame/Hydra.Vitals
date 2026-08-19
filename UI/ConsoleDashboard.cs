using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hydra.Vitals.Core;
using Hydra.Vitals.Models;
using Hydra.Vitals.Services;

namespace Hydra.Vitals.UI
{
    public class ConsoleDashboard
    {
        private readonly IVitalAnalysisService _service;

        public ConsoleDashboard(IVitalAnalysisService service)
        {
            _service = service;
        }

        private static void SafeClear()
        {
            try
            {
                if (!Console.IsOutputRedirected)
                    Console.Clear();
            }
            catch { }
        }

        public async Task RunAsync()
        {
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

            while (true)
            {
                SafeClear();
                DrawHeader();
                await DrawQuickStatsAsync();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("=== ANA MENU ===");
                Console.ResetColor();
                Console.WriteLine("[1] Tum Vitals Kayitlarini Listele");
                Console.WriteLine("[2] ANR (Application Not Responding) Kayitlari & Cozumleri");
                Console.WriteLine("[3] Crash (Yerel / Yonetilen Cokmeler) & Kok Nedenleri");
                Console.WriteLine("[4] Cold Start / Jank / Performans Kayitlari");
                Console.WriteLine("[5] Projeye Gore Filtrele (Blocked, PaintTrek, PanzerLab vb.)");
                Console.WriteLine("[6] Cihaz & Android Surumune Gore Ara (Vivo, Samsung, A8.1 vb.)");
                Console.WriteLine("[7] AI Hizli Danisma & Akilli Arama (Stack Trace / Hata Imzasi)");
                Console.WriteLine("[8] Yeni Vital Kaydi / Hata Cozumu Ekle");
                Console.WriteLine("[9] Detayli Istatistikler & Cozum Oranlari");
                Console.WriteLine("[0] Cikis");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Seciminiz [0-9]: ");
                Console.ResetColor();

                var key = Console.ReadLine()?.Trim();
                if (key == "0")
                {
                    Console.WriteLine("\nHydra.Vitals sonlandiriliyor. Iyi calismalar!");
                    break;
                }

                switch (key)
                {
                    case "1":
                        await ListIssuesAsync(await _service.GetAllIssuesAsync(), "TUM VITALS KAYITLARI");
                        break;
                    case "2":
                        await ListIssuesAsync(await _service.GetIssuesByTypeAsync(VitalType.ANR), "ANR KAYITLARI & COZUM REHBERI");
                        break;
                    case "3":
                        await ListIssuesAsync(await _service.GetIssuesByTypeAsync(VitalType.Crash), "CRASH (COKME) KAYITLARI & ANALIZLERI");
                        break;
                    case "4":
                        var perfIssues = (await _service.GetAllIssuesAsync())
                            .Where(x => x.Type == VitalType.ColdStart || x.Type == VitalType.HotStart || x.Type == VitalType.Jank || x.Type == VitalType.MemoryLeak);
                        await ListIssuesAsync(perfIssues, "PERFORMANS, ACILIS & BELLEK KAYITLARI");
                        break;
                    case "5":
                        await FilterByProjectAsync();
                        break;
                    case "6":
                        await SearchByDeviceAsync();
                        break;
                    case "7":
                        await AiSmartSearchAsync();
                        break;
                    case "8":
                        await AddNewIssueInteractiveAsync();
                        break;
                    case "9":
                        await ShowDetailedStatisticsAsync();
                        break;
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\nGecersiz secim! Devam etmek icin bir tusa basin.");
                        Console.ResetColor();
                        Console.ReadKey();
                        break;
                }
            }
        }

        private void DrawHeader()
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(@"
  _    _             _                 __      ___ _        _     
 | |  | |           | |                \ \    / (_) |      | |    
 | |__| |_   _  __| |_ __ __ _         \ \  / / _| |_ __ _| |___ 
 |  __  | | | |/ _` | '__/ _` |  ______ \ \/ / | | __/ _` | / __|
 | |  | | |_| | (_| | | | (_| | |______| \  /  | | || (_| | \__ \
 |_|  |_|\__, |\__,_|_|  \__,_|           \/   |_|\__\__,_|_|___/
          __/ |                                                   
         |___/      Knowledge Base for Mobile ANR & Crash Solutions");
            Console.ResetColor();
            Console.WriteLine(new string('-', 78));
        }

        private async Task DrawQuickStatsAsync()
        {
            var stats = await _service.GetStatisticsAsync();
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(" [OZET] Toplam Proje: ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(stats.TotalProjects);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(" | Kayitli Vital: ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(stats.TotalIssues);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(" (ANR: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(stats.AnrCount);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(", Crash: ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(stats.CrashCount);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(", Cozulen/Kapali: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(stats.FixedCount + stats.NonActionableCount);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(")");
            Console.ResetColor();
            Console.WriteLine(new string('-', 78));
        }

        private async Task ListIssuesAsync(IEnumerable<VitalIssue> issues, string title)
        {
            var list = issues.ToList();
            SafeClear();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"=== {title} ({list.Count} Kayit) ===");
            Console.ResetColor();

            if (!list.Any())
            {
                Console.WriteLine("\nEslisen kayit bulunamadi.");
                Console.WriteLine("\nDevam etmek icin bir tusa basin...");
                Console.ReadKey();
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"[{i + 1}] ");
                
                SetTypeColor(item.Type);
                Console.Write($"[{item.Type}] ");
                
                SetStatusColor(item.Status);
                Console.Write($"[{item.Status}] ");

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{item.Code}");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($" - {item.Name} ({item.ProjectName})");
            }
            Console.ResetColor();

            Console.WriteLine("\nDetayini gormek istediginiz kaydin numarasini girin (veya geri donmek icin Enter): ");
            var input = Console.ReadLine()?.Trim();
            if (int.TryParse(input, out int selectedIndex) && selectedIndex >= 1 && selectedIndex <= list.Count)
            {
                await ShowIssueDetailAsync(list[selectedIndex - 1]);
            }
        }

        private async Task ShowIssueDetailAsync(VitalIssue issue)
        {
            await Task.Yield();
            SafeClear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==============================================================================");
            Console.WriteLine($" VITAL DETAYI: {issue.Code}");
            Console.WriteLine("==============================================================================");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"Baslik          : {issue.Name}");
            Console.WriteLine($"Proje / Paket   : {issue.ProjectName}");
            Console.Write($"Tur / Siddet    : ");
            SetTypeColor(issue.Type);
            Console.Write(issue.Type);
            Console.ResetColor();
            Console.Write(" / Alt Tur: " + issue.Subtype);
            Console.Write(" / Siddet: ");
            SetSeverityColor(issue.Severity);
            Console.WriteLine(issue.Severity);
            Console.ResetColor();

            Console.Write($"Durum           : ");
            SetStatusColor(issue.Status);
            Console.WriteLine(issue.Status);
            Console.ResetColor();

            if (issue.DetectedDate.HasValue)
                Console.WriteLine($"Tespit Tarihi   : {issue.DetectedDate.Value:yyyy-MM-dd} (Surum: {issue.ReportedVersion ?? "N/A"})");

            Console.WriteLine($"Kullanici/Olay  : {issue.AffectedUsers} Kullanici / {issue.EventCount} Olay (Olay/Kullanici: {issue.EventsPerUser:F2})");

            if (issue.TechnologiesInvolved.Any())
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"Teknolojiler    : {string.Join(", ", issue.TechnologiesInvolved)}");
                Console.ResetColor();
            }

            if (issue.Devices.Any())
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("\n[ETKILENEN CIHAZLAR & ANDROID SURUMLERI]");
                Console.ResetColor();
                foreach (var dev in issue.Devices)
                {
                    Console.WriteLine($"  * {dev.Manufacturer} {dev.Model} (Android {dev.AndroidVersion}{(dev.ApiLevel.HasValue ? $", API {dev.ApiLevel}" : "")}) - {dev.SocGpu ?? "Genel"}");
                }
            }

            if (issue.SignatureFrames.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[HATA IMZASI / STACK FRAMES]");
                Console.ResetColor();
                foreach (var frame in issue.SignatureFrames)
                {
                    Console.WriteLine($"  -> {frame}");
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[KOK NEDEN (ROOT CAUSE)]");
            Console.ResetColor();
            Console.WriteLine(issue.RootCause);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[UYGULANAN / ONERILEN COZUM (FIX APPROACH)]");
            Console.ResetColor();
            Console.WriteLine(issue.FixApproach);

            if (!string.IsNullOrWhiteSpace(issue.LessonsLearned))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n[AI & GELISTIRICI DERSLERI / TUYOLAR (LESSONS LEARNED)]");
                Console.ResetColor();
                Console.WriteLine(issue.LessonsLearned);
            }

            if (!string.IsNullOrWhiteSpace(issue.RelatedDoc))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"\nIlgili Dokuman : {issue.RelatedDoc}");
                Console.ResetColor();
            }

            Console.WriteLine("\n" + new string('-', 78));
            Console.WriteLine("Geri donmek icin bir tusa basin...");
            Console.ReadKey();
        }

        private async Task FilterByProjectAsync()
        {
            var projects = (await _service.GetAllProjectsAsync()).ToList();
            SafeClear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== PROJELER ===");
            Console.ResetColor();

            for (int i = 0; i < projects.Count; i++)
            {
                var p = projects[i];
                Console.WriteLine($"[{i + 1}] {p.Name} ({p.PackageName}) - Platform: {p.PlatformType}");
            }

            Console.Write("\nIncelemek istediginiz proje numarasi: ");
            var input = Console.ReadLine()?.Trim();
            if (int.TryParse(input, out int idx) && idx >= 1 && idx <= projects.Count)
            {
                var selected = projects[idx - 1];
                var issues = await _service.GetIssuesByProjectAsync(selected.Name ?? "");
                await ListIssuesAsync(issues, $"PROJE: {selected.Name}");
            }
        }

        private async Task SearchByDeviceAsync()
        {
            SafeClear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== CIHAZ & ANDROID SURUMU ARAMA ===");
            Console.ResetColor();
            Console.Write("\nCihaz modeli, uretici (Samsung, Vivo, Oppo) veya Android surumu (8.1, 13, 14) girin: ");
            var query = Console.ReadLine()?.Trim();

            if (!string.IsNullOrWhiteSpace(query))
            {
                var results = await _service.SearchIssuesAsync(query);
                await ListIssuesAsync(results, $"ARAMA: '{query}'");
            }
        }

        private async Task AiSmartSearchAsync()
        {
            SafeClear();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(@"
    ___    ____   ____                               __  __          __   
   /   |  /  _/  / __ \____ _____  __  ___________ _/ /_/ /_  ____  / /__ 
  / /| |  / /   / / / / __ `/ __ \/ / / / ___/ __ `/ __/ __ \/ __ \/ //_/ 
 / ___ |_/ /   / /_/ / /_/ / / / / /_/ (__  ) /_/ / /_/ / / / /_/ / ,<    
/_/  |_/___/  /_____/\__,_/_/ /_/\__,_/____/\__,_/\__/_/ /_/\____/_/|_|   
");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Stack trace satiri, hata metodu (orn: EglManager, nativePollOnce, SpriteFont, Exit, inflate_fast) veya anahtar kelime girin:");
            Console.ResetColor();
            Console.Write("\nSorgu: ");
            var query = Console.ReadLine()?.Trim();

            if (!string.IsNullOrWhiteSpace(query))
            {
                var matches = (await _service.SearchIssuesAsync(query)).ToList();
                await ListIssuesAsync(matches, $"AI ESLESEN VITAL COZUMLERI ('{query}')");
            }
        }

        private async Task AddNewIssueInteractiveAsync()
        {
            SafeClear();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("=== YENI VITAL / COZUM KAYDI EKLE ===");
            Console.ResetColor();

            Console.Write("Hata Kodu (orn. anr-custom-freeze, crash-null-texture): ");
            var code = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(code)) return;

            Console.Write("Baslik: ");
            var title = Console.ReadLine()?.Trim() ?? code;

            Console.Write("Proje Adi (Blocked, PaintTrek vb.): ");
            var projectName = Console.ReadLine()?.Trim() ?? "Blocked";

            Console.WriteLine("Tur: [1] ANR, [2] Crash, [3] MemoryLeak, [4] ColdStart, [5] Jank");
            Console.Write("Secim: ");
            var typeChoice = Console.ReadLine()?.Trim();
            var vitalType = typeChoice switch
            {
                "1" => VitalType.ANR,
                "2" => VitalType.Crash,
                "3" => VitalType.MemoryLeak,
                "4" => VitalType.ColdStart,
                "5" => VitalType.Jank,
                _ => VitalType.ANR
            };

            Console.Write("Alt Tur / Hata Sinifi (orn. NullReferenceException, Input dispatching timed out): ");
            var subtype = Console.ReadLine()?.Trim() ?? "General";

            Console.Write("Kok Neden (Root Cause): ");
            var rootCause = Console.ReadLine()?.Trim() ?? "Henuz analiz edilmedi.";

            Console.Write("Uygulanan / Onerilen Cozum (Fix Approach): ");
            var fixApproach = Console.ReadLine()?.Trim() ?? "Arastiriliyor.";

            Console.Write("AI / Gelistirici Tuyosu (Lessons Learned): ");
            var lessons = Console.ReadLine()?.Trim();

            var issue = new VitalIssue(
                code,
                title,
                Guid.NewGuid(),
                projectName,
                vitalType,
                subtype,
                VitalSeverity.Medium,
                VitalStatus.Open
            )
            {
                DetectedDate = DateTime.UtcNow,
                RootCause = rootCause,
                FixApproach = fixApproach,
                LessonsLearned = lessons
            };

            await _service.AddOrUpdateIssueAsync(issue);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nKayit basariyla eklendi! Devam etmek icin bir tusa basin.");
            Console.ResetColor();
            Console.ReadKey();
        }

        private async Task ShowDetailedStatisticsAsync()
        {
            SafeClear();
            var stats = await _service.GetStatisticsAsync();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== HYDRA.VITALS BILGI BANKASI ISTATISTIKLERI ===");
            Console.ResetColor();
            Console.WriteLine($"Kayitli Proje Sayisi       : {stats.TotalProjects}");
            Console.WriteLine($"Toplam Vital Sorun Sayisi   : {stats.TotalIssues}");
            Console.WriteLine($"ANR Sayisi                  : {stats.AnrCount}");
            Console.WriteLine($"Crash (Cokme) Sayisi        : {stats.CrashCount}");
            Console.WriteLine($"Bellek / Acilis Sorunlari   : {stats.MemoryLeakCount + stats.StartupLagCount}");
            Console.WriteLine($"Cozulen / Dogrulanan        : {stats.FixedCount}");
            Console.WriteLine($"Azaltilan (Mitigated)       : {stats.MitigatedCount}");
            Console.WriteLine($"Aksiyon Disi (OS / Late)    : {stats.NonActionableCount}");
            Console.WriteLine($"Acik / Izlemede             : {stats.OpenCount}");
            Console.WriteLine($"Toplam Etkilenen Kullanici  : {stats.TotalAffectedUsers}");
            Console.WriteLine($"Toplam Olay (Events)        : {stats.TotalEvents}");

            Console.WriteLine("\n" + new string('-', 78));
            Console.WriteLine("Devam etmek icin bir tusa basin...");
            Console.ReadKey();
        }

        private static void SetTypeColor(VitalType type)
        {
            Console.ForegroundColor = type switch
            {
                VitalType.Crash => ConsoleColor.Red,
                VitalType.ANR => ConsoleColor.Yellow,
                VitalType.MemoryLeak => ConsoleColor.Magenta,
                VitalType.ColdStart or VitalType.HotStart => ConsoleColor.Cyan,
                _ => ConsoleColor.White
            };
        }

        private static void SetSeverityColor(VitalSeverity severity)
        {
            Console.ForegroundColor = severity switch
            {
                VitalSeverity.Critical => ConsoleColor.Red,
                VitalSeverity.High => ConsoleColor.DarkRed,
                VitalSeverity.Medium => ConsoleColor.Yellow,
                VitalSeverity.Low => ConsoleColor.Blue,
                _ => ConsoleColor.Gray
            };
        }

        private static void SetStatusColor(VitalStatus status)
        {
            Console.ForegroundColor = status switch
            {
                VitalStatus.FixedVerified => ConsoleColor.Green,
                VitalStatus.FixedAwaitingRelease => ConsoleColor.DarkGreen,
                VitalStatus.Mitigated or VitalStatus.PartiallyMitigated => ConsoleColor.Cyan,
                VitalStatus.ClosedNotActionable or VitalStatus.ClosedOsDriverBug => ConsoleColor.DarkGray,
                VitalStatus.FrameworkMonitored => ConsoleColor.DarkYellow,
                VitalStatus.Open => ConsoleColor.Red,
                _ => ConsoleColor.Gray
            };
        }
    }
}