using System;
using System.Collections.Generic;
using Hydra.Vitals.Core;

namespace Hydra.Vitals.Models
{
    public class AppProject : BaseObject<AppProject>
    {
        public string PackageName { get; set; } = string.Empty;
        public string TargetStore { get; set; } = "Google Play";
        public ProjectPlatformType PlatformType { get; set; } = ProjectPlatformType.MobileAndroid;
        public List<string> Technologies { get; set; } = new();
        public string? RepositoryUrl { get; set; }

        public AppProject()
        {
        }

        public AppProject(string name, string packageName, ProjectPlatformType platformType = ProjectPlatformType.MobileAndroid, string? description = null)
        {
            Initialize();
            Name = name;
            PackageName = packageName;
            PlatformType = platformType;
            Description = description;
        }

        public AppProject AddTechnology(string technology)
        {
            if (!string.IsNullOrWhiteSpace(technology) && !Technologies.Contains(technology))
            {
                Technologies.Add(technology);
                ModifiedDate = DateTime.UtcNow;
            }
            return this;
        }
    }
}
