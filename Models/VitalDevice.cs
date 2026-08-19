using System;
using System.Collections.Generic;
using Hydra.Vitals.Core;

namespace Hydra.Vitals.Models
{
    public class VitalDevice : BaseObject<VitalDevice>
    {
        public string Model { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string AndroidVersion { get; set; } = string.Empty;
        public int? ApiLevel { get; set; }
        public string? SocGpu { get; set; }
        public int? RamGb { get; set; }
        public string? Abi { get; set; }
        public List<string> NativeLibraries { get; set; } = new();
        public string? OsFrameworkNotes { get; set; }

        public VitalDevice()
        {
        }

        public VitalDevice(string model, string manufacturer, string androidVersion, int? apiLevel = null, string? socGpu = null)
        {
            Initialize();
            Model = model;
            Manufacturer = manufacturer;
            AndroidVersion = androidVersion;
            ApiLevel = apiLevel;
            SocGpu = socGpu;
            Name = $"{manufacturer} {model} (Android {androidVersion})";
        }

        public override string ToString()
        {
            return $"{Manufacturer} {Model} [Android {AndroidVersion}{(ApiLevel.HasValue ? $", API {ApiLevel}" : "")}]";
        }
    }
}
