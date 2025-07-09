using System.Collections.Generic;
using System.Net;
using NzbDrone.Core.Exceptions;

namespace NzbDrone.Core.Backup
{
    public class BackupMigrationConflictException : NzbDroneClientException
    {
        public List<ConfigurationConflict> Conflicts { get; }
        public bool IsLegacyReadarrBackup { get; }

        public BackupMigrationConflictException(List<ConfigurationConflict> conflicts, bool isLegacyReadarrBackup)
            : base(HttpStatusCode.Conflict, BuildMessage(conflicts, isLegacyReadarrBackup))
        {
            Conflicts = conflicts;
            IsLegacyReadarrBackup = isLegacyReadarrBackup;
        }

        private static string BuildMessage(List<ConfigurationConflict> conflicts, bool isLegacyReadarrBackup)
        {
            var source = isLegacyReadarrBackup ? "legacy Readarr" : "different ReadAIrr version";
            return $"Configuration conflicts detected while restoring backup from {source}. User intervention required for {conflicts.Count} setting(s).";
        }
    }

    public class ConfigurationConflict
    {
        public string Setting { get; set; }
        public string CurrentValue { get; set; }
        public string BackupValue { get; set; }
        public string Description { get; set; }
        public ConflictType Type { get; set; }
        public List<string> SuggestedActions { get; set; } = new List<string>();
    }

    public enum ConflictType
    {
        PortConfiguration,
        ApplicationName,
        DatabaseConfiguration,
        PathConfiguration,
        Other
    }
}
