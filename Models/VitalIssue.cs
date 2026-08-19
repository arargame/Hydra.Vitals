using System;
using System.Collections.Generic;
using Hydra.Vitals.Core;

namespace Hydra.Vitals.Models
{
    public class VitalIssue : BaseObject<VitalIssue>
    {
        public string Code { get; set; } = string.Empty;
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        
        public VitalType Type { get; set; } = VitalType.ANR;
        public string Subtype { get; set; } = string.Empty;
        public VitalSeverity Severity { get; set; } = VitalSeverity.Medium;
        public VitalStatus Status { get; set; } = VitalStatus.Open;

        public DateTime? DetectedDate { get; set; }
        public string? ReportedVersion { get; set; }
        public int AffectedUsers { get; set; }
        public int EventCount { get; set; }
        public double EventsPerUser => AffectedUsers > 0 ? (double)EventCount / AffectedUsers : 0;

        public List<VitalDevice> Devices { get; set; } = new();
        public List<string> SignatureFrames { get; set; } = new();
        public List<string> TechnologiesInvolved { get; set; } = new();
        
        public string? FullStackTrace { get; set; }
        public string RootCause { get; set; } = string.Empty;
        public string FixApproach { get; set; } = string.Empty;
        public string? LessonsLearned { get; set; }
        public string? AiDiagnosticsGuidance { get; set; }
        public string? RelatedDoc { get; set; }
        public string? DuplicateOfCode { get; set; }

        public VitalIssue()
        {
        }

        public VitalIssue(string code, string title, Guid projectId, string projectName, VitalType type, string subtype, VitalSeverity severity, VitalStatus status)
        {
            Initialize();
            Code = code;
            Name = title;
            ProjectId = projectId;
            ProjectName = projectName;
            Type = type;
            Subtype = subtype;
            Severity = severity;
            Status = status;
        }

        public VitalIssue AddDevice(VitalDevice device)
        {
            if (device != null)
            {
                Devices.Add(device);
                ModifiedDate = DateTime.UtcNow;
            }
            return this;
        }

        public VitalIssue AddSignatureFrame(string frame)
        {
            if (!string.IsNullOrWhiteSpace(frame) && !SignatureFrames.Contains(frame))
            {
                SignatureFrames.Add(frame);
                ModifiedDate = DateTime.UtcNow;
            }
            return this;
        }

        public VitalIssue AddTechnology(string tech)
        {
            if (!string.IsNullOrWhiteSpace(tech) && !TechnologiesInvolved.Contains(tech))
            {
                TechnologiesInvolved.Add(tech);
                ModifiedDate = DateTime.UtcNow;
            }
            return this;
        }

        public override string ToString()
        {
            return $"[{Type}] [{Severity}] [{Status}] {Code} - {Name} ({ProjectName})";
        }
    }
}
