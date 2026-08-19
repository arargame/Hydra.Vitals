using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hydra.Vitals.Core;
using Hydra.Vitals.Models;

namespace Hydra.Vitals.Services
{
    public class VitalsStatistics
    {
        public int TotalProjects { get; set; }
        public int TotalIssues { get; set; }
        public int AnrCount { get; set; }
        public int CrashCount { get; set; }
        public int MemoryLeakCount { get; set; }
        public int StartupLagCount { get; set; }
        public int FixedCount { get; set; }
        public int MitigatedCount { get; set; }
        public int NonActionableCount { get; set; }
        public int OpenCount { get; set; }
        public int TotalAffectedUsers { get; set; }
        public int TotalEvents { get; set; }
    }

    public interface IVitalAnalysisService
    {
        Task<IEnumerable<VitalIssue>> GetAllIssuesAsync();
        Task<IEnumerable<VitalIssue>> GetIssuesByTypeAsync(VitalType type);
        Task<IEnumerable<VitalIssue>> GetIssuesByProjectAsync(string projectName);
        Task<IEnumerable<VitalIssue>> GetIssuesByStatusAsync(VitalStatus status);
        Task<IEnumerable<VitalIssue>> SearchIssuesAsync(string query);
        Task<VitalIssue?> GetIssueByCodeAsync(string code);
        Task<VitalsStatistics> GetStatisticsAsync();
        Task<IEnumerable<AppProject>> GetAllProjectsAsync();
        Task<AppProject> AddProjectAsync(AppProject project);
        Task<VitalIssue> AddOrUpdateIssueAsync(VitalIssue issue);
    }
}
