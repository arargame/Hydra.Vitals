using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hydra.Vitals.Core;
using Hydra.Vitals.Data;
using Hydra.Vitals.Models;

namespace Hydra.Vitals.Services
{
    public class VitalAnalysisService : IVitalAnalysisService
    {
        private readonly IVitalRepository<VitalIssue> _issueRepository;
        private readonly IVitalRepository<AppProject> _projectRepository;

        public VitalAnalysisService(IVitalRepository<VitalIssue> issueRepository, IVitalRepository<AppProject> projectRepository)
        {
            _issueRepository = issueRepository;
            _projectRepository = projectRepository;
        }

        public async Task<IEnumerable<VitalIssue>> GetAllIssuesAsync()
        {
            return await _issueRepository.GetAllAsync();
        }

        public async Task<IEnumerable<VitalIssue>> GetIssuesByTypeAsync(VitalType type)
        {
            return await _issueRepository.FindAsync(x => x.Type == type);
        }

        public async Task<IEnumerable<VitalIssue>> GetIssuesByProjectAsync(string projectName)
        {
            return await _issueRepository.FindAsync(x => 
                string.Equals(x.ProjectName, projectName, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<IEnumerable<VitalIssue>> GetIssuesByStatusAsync(VitalStatus status)
        {
            return await _issueRepository.FindAsync(x => x.Status == status);
        }

        public async Task<IEnumerable<VitalIssue>> SearchIssuesAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return await GetAllIssuesAsync();

            query = query.Trim().ToLowerInvariant();

            return await _issueRepository.FindAsync(x =>
                (x.Name != null && x.Name.ToLowerInvariant().Contains(query)) ||
                (x.Code != null && x.Code.ToLowerInvariant().Contains(query)) ||
                (x.Subtype != null && x.Subtype.ToLowerInvariant().Contains(query)) ||
                (x.RootCause != null && x.RootCause.ToLowerInvariant().Contains(query)) ||
                (x.FixApproach != null && x.FixApproach.ToLowerInvariant().Contains(query)) ||
                (x.LessonsLearned != null && x.LessonsLearned.ToLowerInvariant().Contains(query)) ||
                x.SignatureFrames.Any(f => f.ToLowerInvariant().Contains(query)) ||
                x.TechnologiesInvolved.Any(t => t.ToLowerInvariant().Contains(query)) ||
                x.Devices.Any(d => (d.Model != null && d.Model.ToLowerInvariant().Contains(query)) || 
                                   (d.Manufacturer != null && d.Manufacturer.ToLowerInvariant().Contains(query)) ||
                                   (d.SocGpu != null && d.SocGpu.ToLowerInvariant().Contains(query)))
            );
        }

        public async Task<VitalIssue?> GetIssueByCodeAsync(string code)
        {
            var issues = await _issueRepository.FindAsync(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
            return issues.FirstOrDefault();
        }

        public async Task<VitalsStatistics> GetStatisticsAsync()
        {
            var issues = (await _issueRepository.GetAllAsync()).ToList();
            var projects = (await _projectRepository.GetAllAsync()).ToList();

            return new VitalsStatistics
            {
                TotalProjects = projects.Count,
                TotalIssues = issues.Count,
                AnrCount = issues.Count(x => x.Type == VitalType.ANR),
                CrashCount = issues.Count(x => x.Type == VitalType.Crash),
                MemoryLeakCount = issues.Count(x => x.Type == VitalType.MemoryLeak),
                StartupLagCount = issues.Count(x => x.Type == VitalType.ColdStart || x.Type == VitalType.HotStart),
                FixedCount = issues.Count(x => x.Status == VitalStatus.FixedVerified || x.Status == VitalStatus.FixedAwaitingRelease),
                MitigatedCount = issues.Count(x => x.Status == VitalStatus.Mitigated || x.Status == VitalStatus.PartiallyMitigated),
                NonActionableCount = issues.Count(x => x.Status == VitalStatus.ClosedNotActionable || x.Status == VitalStatus.ClosedOsDriverBug || x.Status == VitalStatus.Duplicate),
                OpenCount = issues.Count(x => x.Status == VitalStatus.Open || x.Status == VitalStatus.FrameworkMonitored),
                TotalAffectedUsers = issues.Sum(x => x.AffectedUsers),
                TotalEvents = issues.Sum(x => x.EventCount)
            };
        }

        public async Task<IEnumerable<AppProject>> GetAllProjectsAsync()
        {
            return await _projectRepository.GetAllAsync();
        }

        public async Task<AppProject> AddProjectAsync(AppProject project)
        {
            return await _projectRepository.AddAsync(project);
        }

        public async Task<VitalIssue> AddOrUpdateIssueAsync(VitalIssue issue)
        {
            var existing = await GetIssueByCodeAsync(issue.Code);
            if (existing != null)
            {
                issue.Id = existing.Id;
                return await _issueRepository.UpdateAsync(issue);
            }
            return await _issueRepository.AddAsync(issue);
        }
    }
}
