using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Hydra.Vitals.Models;

namespace Hydra.Vitals.Data
{
    public class VitalsDatabasePayload
    {
        public int SchemaVersion { get; set; } = 1;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public string Note { get; set; } = "Hydra.Vitals - Unified AI & Developer Knowledge Base for Mobile Performance, ANRs, Crashes and Leaks";
        public List<AppProject> Projects { get; set; } = new();
        public List<VitalIssue> Issues { get; set; } = new();
    }

    public class JsonDatabaseContext
    {
        private readonly string _databaseFilePath;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private VitalsDatabasePayload _payload = new();
        private bool _isLoaded = false;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public JsonDatabaseContext(string? dbPath = null)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                _databaseFilePath = Path.Combine(baseDir, "vitals_database.json");
            }
            else
            {
                _databaseFilePath = dbPath;
            }
        }

        public string DatabasePath => _databaseFilePath;

        public async Task<VitalsDatabasePayload> LoadAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (_isLoaded)
                    return _payload;

                if (File.Exists(_databaseFilePath))
                {
                    var json = await File.ReadAllTextAsync(_databaseFilePath);
                    var deserialized = JsonSerializer.Deserialize<VitalsDatabasePayload>(json, _jsonOptions);
                    if (deserialized != null)
                    {
                        _payload = deserialized;
                    }
                }
                else
                {
                    _payload = new VitalsDatabasePayload();
                }

                _isLoaded = true;
                return _payload;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SaveAsync()
        {
            await _lock.WaitAsync();
            try
            {
                _payload.LastUpdated = DateTime.UtcNow;
                var json = JsonSerializer.Serialize(_payload, _jsonOptions);
                var dir = Path.GetDirectoryName(_databaseFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                await File.WriteAllTextAsync(_databaseFilePath, json);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<List<AppProject>> GetProjectsAsync()
        {
            var payload = await LoadAsync();
            return payload.Projects;
        }

        public async Task<List<VitalIssue>> GetIssuesAsync()
        {
            var payload = await LoadAsync();
            return payload.Issues;
        }
    }
}
