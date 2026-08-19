using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hydra.Vitals.Core;
using Hydra.Vitals.Models;

namespace Hydra.Vitals.Data
{
    public class ProjectJsonRepository : IVitalRepository<AppProject>
    {
        private readonly JsonDatabaseContext _context;

        public ProjectJsonRepository(JsonDatabaseContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<AppProject>> GetAllAsync()
        {
            var list = await _context.GetProjectsAsync();
            return list.Where(x => x.IsActive).ToList();
        }

        public async Task<AppProject?> GetByIdAsync(Guid id)
        {
            var list = await _context.GetProjectsAsync();
            return list.FirstOrDefault(x => x.Id == id);
        }

        public async Task<IEnumerable<AppProject>> FindAsync(Func<AppProject, bool> predicate)
        {
            var list = await _context.GetProjectsAsync();
            return list.Where(predicate).ToList();
        }

        public async Task<AppProject> AddAsync(AppProject entity)
        {
            var list = await _context.GetProjectsAsync();
            entity.AddedDate = DateTime.UtcNow;
            entity.ModifiedDate = DateTime.UtcNow;
            list.Add(entity);
            await _context.SaveAsync();
            return entity;
        }

        public async Task<AppProject> UpdateAsync(AppProject entity)
        {
            var list = await _context.GetProjectsAsync();
            var index = list.FindIndex(x => x.Id == entity.Id);
            if (index >= 0)
            {
                entity.ModifiedDate = DateTime.UtcNow;
                list[index] = entity;
                await _context.SaveAsync();
            }
            return entity;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var list = await _context.GetProjectsAsync();
            var item = list.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                item.IsActive = false;
                item.ModifiedDate = DateTime.UtcNow;
                await _context.SaveAsync();
                return true;
            }
            return false;
        }

        public async Task<int> CountAsync()
        {
            var list = await _context.GetProjectsAsync();
            return list.Count(x => x.IsActive);
        }
    }

    public class VitalIssueJsonRepository : IVitalRepository<VitalIssue>
    {
        private readonly JsonDatabaseContext _context;

        public VitalIssueJsonRepository(JsonDatabaseContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<VitalIssue>> GetAllAsync()
        {
            var list = await _context.GetIssuesAsync();
            return list.Where(x => x.IsActive).OrderByDescending(x => x.DetectedDate ?? x.AddedDate).ToList();
        }

        public async Task<VitalIssue?> GetByIdAsync(Guid id)
        {
            var list = await _context.GetIssuesAsync();
            return list.FirstOrDefault(x => x.Id == id);
        }

        public async Task<IEnumerable<VitalIssue>> FindAsync(Func<VitalIssue, bool> predicate)
        {
            var list = await _context.GetIssuesAsync();
            return list.Where(predicate).ToList();
        }

        public async Task<VitalIssue> AddAsync(VitalIssue entity)
        {
            var list = await _context.GetIssuesAsync();
            entity.AddedDate = DateTime.UtcNow;
            entity.ModifiedDate = DateTime.UtcNow;
            list.Add(entity);
            await _context.SaveAsync();
            return entity;
        }

        public async Task<VitalIssue> UpdateAsync(VitalIssue entity)
        {
            var list = await _context.GetIssuesAsync();
            var index = list.FindIndex(x => x.Id == entity.Id || (!string.IsNullOrEmpty(x.Code) && x.Code == entity.Code));
            if (index >= 0)
            {
                entity.ModifiedDate = DateTime.UtcNow;
                list[index] = entity;
                await _context.SaveAsync();
            }
            return entity;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var list = await _context.GetIssuesAsync();
            var item = list.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                item.IsActive = false;
                item.ModifiedDate = DateTime.UtcNow;
                await _context.SaveAsync();
                return true;
            }
            return false;
        }

        public async Task<int> CountAsync()
        {
            var list = await _context.GetIssuesAsync();
            return list.Count(x => x.IsActive);
        }
    }
}
