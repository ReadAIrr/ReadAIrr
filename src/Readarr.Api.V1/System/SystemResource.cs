using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Authentication;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Update;

namespace Readarr.Api.V1.System
{
    public class SystemResource
    {
        public string AppName { get; set; }
        public string InstanceName { get; set; }
        public string Version { get; set; }
        public DateTime BuildTime { get; set; }
        public bool IsDebug { get; set; }
        public bool IsProduction { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsUserInteractive { get; set; }
        public string StartupPath { get; set; }
        public string AppData { get; set; }
        public string OsName { get; set; }
        public string OsVersion { get; set; }
        public bool IsNetCore { get; set; }
        public bool IsLinux { get; set; }
        public bool IsOsx { get; set; }
        public bool IsWindows { get; set; }
        public bool IsDocker { get; set; }
        public RuntimeMode Mode { get; set; }
        public string Branch { get; set; }
        public DatabaseType DatabaseType { get; set; }
        public Version DatabaseVersion { get; set; }
        public AuthenticationType Authentication { get; set; }
        public int MigrationVersion { get; set; }
        public string UrlBase { get; set; }
        public Version RuntimeVersion { get; set; }
        public string RuntimeName { get; set; }
        public DateTime StartTime { get; set; }
        public string PackageVersion { get; set; }
        public string PackageAuthor { get; set; }
        public UpdateMechanism PackageUpdateMechanism { get; set; }
        public string PackageUpdateMechanismMessage { get; set; }
        public DatabaseStatusResource DatabaseStatus { get; set; }
    }

    public class DatabaseStatusResource
    {
        public string ActiveProvider { get; set; }
        public string ReadinessState { get; set; }
        public string ReadinessLabel { get; set; }
        public string NextStep { get; set; }
        public string BackupRecommendation { get; set; }
        public string SqlitePath { get; set; }
        public long? SqliteSizeBytes { get; set; }
        public string SqliteSizeLabel { get; set; }
        public PostgresConfigStatusResource Postgres { get; set; }
        public string MigrationGuidance { get; set; }
        public string RedactedTarget { get; set; }
        public string EnvironmentExample { get; set; }
        public string ConfigXmlExample { get; set; }
        public string CopyableChecklist { get; set; }
        public List<string> Warnings { get; set; }
        public List<string> Checklist { get; set; }
    }

    public class MetadataServiceStatusResource
    {
        public string MetadataSource { get; set; }
        public string SourceLabel { get; set; }
        public string SourceType { get; set; }
        public string ServiceUrl { get; set; }
        public string ReadinessState { get; set; }
        public string ReadinessLabel { get; set; }
        public bool IsReachable { get; set; }
        public string HealthMessage { get; set; }
        public string HealthDetail { get; set; }
        public int? StatusCode { get; set; }
        public double ResponseTimeMs { get; set; }
        public DateTime StatusCheckedAt { get; set; }
        public string UpdateEndpoint { get; set; }
        public string UpdateBranch { get; set; }
        public string CurrentVersion { get; set; }
        public bool? UpdateAvailable { get; set; }
        public string LatestVersion { get; set; }
        public DateTime? LatestReleaseDate { get; set; }
        public string UpdateCheckMessage { get; set; }
        public string SidecarManagementMode { get; set; }
        public string SidecarManagementLabel { get; set; }
        public bool SidecarManagedByReadAIrr { get; set; }
        public bool SidecarUpdateSupported { get; set; }
        public string SidecarUpdateAction { get; set; }
        public string SidecarCurrentVersion { get; set; }
        public string SidecarLatestVersion { get; set; }
        public bool? SidecarUpdateAvailable { get; set; }
        public string SidecarVersionMessage { get; set; }
        public string SidecarUpdateCheckMessage { get; set; }
        public string SidecarUpdateGuidance { get; set; }
        public string ConfidenceMode { get; set; }
        public string ConfidenceSummary { get; set; }
        public bool AutomaticMetadataDecisioningEnabled { get; set; }
        public List<MetadataSourceConfidenceSignalResource> ConfidenceSignals { get; set; }
        public List<string> Warnings { get; set; }
        public List<string> Checklist { get; set; }
    }

    public class MetadataSourceConfidenceSignalResource
    {
        public string SourceType { get; set; }
        public string SourceLabel { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public bool IsAvailable { get; set; }
        public int ConfidenceWeight { get; set; }
        public string Status { get; set; }
        public string Explanation { get; set; }
    }

    public class PostgresConfigStatusResource
    {
        public bool IsConfigured { get; set; }
        public bool HostConfigured { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public bool UserConfigured { get; set; }
        public bool PasswordConfigured { get; set; }
        public string MainDatabase { get; set; }
        public string LogDatabase { get; set; }
        public string CacheDatabase { get; set; }
        public bool? IsReachable { get; set; }
        public string ReachabilityMessage { get; set; }
    }

    public static class DatabaseStatusResourceMapper
    {
        private const long LargeSqliteThresholdBytes = 512L * 1024L * 1024L;

        public static DatabaseStatusResource ToResource(DatabaseType activeDatabaseType,
                                                        string sqlitePath,
                                                        bool sqliteExists,
                                                        long? sqliteSizeBytes,
                                                        bool postgresHostConfigured,
                                                        string postgresHost,
                                                        int postgresPort,
                                                        bool postgresUserConfigured,
                                                        bool postgresPasswordConfigured,
                                                        string postgresMainDb,
                                                        string postgresLogDb,
                                                        string postgresCacheDb,
                                                        bool? postgresReachable,
                                                        string postgresReachabilityMessage,
                                                        bool hasBackup)
        {
            var postgresConfigured = postgresHostConfigured;
            var warnings = new List<string>();
            var readinessState = "notConfigured";
            var readinessLabel = "PostgreSQL not configured";
            var nextStep = "Create a current backup, stop the app, configure PostgreSQL, migrate or import the SQLite data, then restart and verify.";

            if (activeDatabaseType == DatabaseType.PostgreSQL)
            {
                readinessState = postgresReachable == false ? "blocked" : "activePostgreSQL";
                readinessLabel = postgresReachable == false ? "PostgreSQL active, readiness check failed" : "Running on PostgreSQL";
                nextStep = "No migration is needed. Keep backups enabled and monitor database health after large imports.";
            }
            else if (postgresConfigured)
            {
                if (!postgresUserConfigured || !postgresPasswordConfigured || string.IsNullOrWhiteSpace(postgresMainDb))
                {
                    readinessState = "blocked";
                    readinessLabel = "PostgreSQL configuration incomplete";
                    nextStep = "Complete the PostgreSQL host, user, password, and database names before migration.";
                }
                else if (postgresReachable == true)
                {
                    readinessState = "reachable";
                    readinessLabel = "PostgreSQL target reachable";
                    nextStep = "Take a fresh backup, stop ReadAIrr, perform the migration using the documented manual process, then restart on PostgreSQL.";
                }
                else if (postgresReachable == false)
                {
                    readinessState = "blocked";
                    readinessLabel = "PostgreSQL target unreachable";
                    nextStep = "Fix PostgreSQL networking, credentials, or database names before attempting migration.";
                }
                else
                {
                    readinessState = "configured";
                    readinessLabel = "PostgreSQL configured";
                    nextStep = "Run a reachability check, then take a fresh backup before attempting migration.";
                }
            }

            if (activeDatabaseType == DatabaseType.SQLite && sqliteExists && sqliteSizeBytes.HasValue && sqliteSizeBytes.Value >= LargeSqliteThresholdBytes)
            {
                warnings.Add("The SQLite database is large. PostgreSQL may improve large-library responsiveness, but migrate only after a verified backup.");
            }

            if (activeDatabaseType == DatabaseType.SQLite && !hasBackup)
            {
                warnings.Add("No backup was found. Create a manual backup before changing database backends.");
            }

            if (activeDatabaseType == DatabaseType.SQLite)
            {
                warnings.Add("ReadAIrr does not perform an automatic live SQLite to PostgreSQL conversion from this page. Treat this as a guided manual checklist.");
            }

            if (postgresConfigured && !postgresUserConfigured)
            {
                warnings.Add("PostgreSQL host is configured but no user is configured.");
            }

            if (postgresConfigured && !postgresPasswordConfigured)
            {
                warnings.Add("PostgreSQL host is configured but no password is configured.");
            }

            if (postgresReachable == false && !string.IsNullOrWhiteSpace(postgresReachabilityMessage))
            {
                warnings.Add(postgresReachabilityMessage);
            }

            var checklist = GetMigrationChecklist(activeDatabaseType, postgresConfigured, postgresReachable, hasBackup);
            var environmentExample = GetEnvironmentExample(postgresHostConfigured ? postgresHost : "postgres.example.internal",
                postgresPort,
                postgresMainDb,
                postgresLogDb,
                postgresCacheDb);

            return new DatabaseStatusResource
            {
                ActiveProvider = activeDatabaseType.ToString(),
                ReadinessState = readinessState,
                ReadinessLabel = readinessLabel,
                NextStep = nextStep,
                BackupRecommendation = activeDatabaseType == DatabaseType.SQLite ? "Back up config.xml and the SQLite database before migration." : "Keep scheduled PostgreSQL and app configuration backups in place.",
                SqlitePath = activeDatabaseType == DatabaseType.SQLite ? sqlitePath : null,
                SqliteSizeBytes = activeDatabaseType == DatabaseType.SQLite && sqliteExists ? sqliteSizeBytes : null,
                SqliteSizeLabel = activeDatabaseType == DatabaseType.SQLite && sqliteExists ? FormatBytes(sqliteSizeBytes ?? 0) : null,
                MigrationGuidance = activeDatabaseType == DatabaseType.PostgreSQL ?
                    "This instance is already running on PostgreSQL. Keep rollback backups and validate imports/search after large metadata changes." :
                    "Use this guide to prepare a manual SQLite to PostgreSQL migration. The app will not change database settings or migrate data from this page.",
                RedactedTarget = postgresConfigured ? $"{postgresHost}:{postgresPort}/{RedactName(postgresMainDb)}" : "PostgreSQL target not configured",
                EnvironmentExample = environmentExample,
                ConfigXmlExample = GetConfigXmlExample(postgresHostConfigured ? postgresHost : "postgres.example.internal",
                    postgresPort,
                    postgresMainDb,
                    postgresLogDb,
                    postgresCacheDb),
                CopyableChecklist = string.Join(Environment.NewLine, checklist.Select((item, index) => $"{index + 1}. {item}")),
                Postgres = new PostgresConfigStatusResource
                {
                    IsConfigured = postgresConfigured,
                    HostConfigured = postgresHostConfigured,
                    Host = postgresHostConfigured ? postgresHost : null,
                    Port = postgresPort,
                    UserConfigured = postgresUserConfigured,
                    PasswordConfigured = postgresPasswordConfigured,
                    MainDatabase = postgresConfigured ? postgresMainDb : null,
                    LogDatabase = postgresConfigured ? postgresLogDb : null,
                    CacheDatabase = postgresConfigured ? postgresCacheDb : null,
                    IsReachable = postgresReachable,
                    ReachabilityMessage = postgresReachabilityMessage
                },
                Warnings = warnings,
                Checklist = checklist
            };
        }

        private static List<string> GetMigrationChecklist(DatabaseType activeDatabaseType, bool postgresConfigured, bool? postgresReachable, bool hasBackup)
        {
            if (activeDatabaseType == DatabaseType.PostgreSQL)
            {
                return new List<string>
                {
                    "Confirm scheduled backups include PostgreSQL and config.xml.",
                    "Keep the last SQLite backup until the PostgreSQL instance has been verified.",
                    "Validate System Status, author/book pages, queue, history, and imports after major upgrades.",
                    "Monitor PostgreSQL storage and connection health during large scans/imports."
                };
            }

            var checklist = new List<string>
            {
                hasBackup ? "Verify the latest backup is recent and restorable." : "Create a fresh app backup from System > Backup before changing anything.",
                "Stop ReadAIrr before switching database backends.",
                postgresConfigured ? "Confirm the configured PostgreSQL host, port, user, and database names." : "Prepare PostgreSQL with dedicated main, log, and cache databases plus a least-privilege user.",
                postgresReachable == true ? "Keep the reachable PostgreSQL target unchanged until migration time." : "Verify PostgreSQL network access and credentials from the ReadAIrr host/container.",
                "Export or migrate the SQLite data using the documented manual process outside ReadAIrr.",
                "Set only non-secret-safe config here; keep the PostgreSQL password in your secret store or deployment environment.",
                "Start ReadAIrr on PostgreSQL and confirm System > Status reports PostgreSQL as active.",
                "Keep the original SQLite/config backup until library scans, imports, queue, history, and metadata refreshes are verified."
            };

            return checklist;
        }

        private static string GetEnvironmentExample(string host, int port, string mainDb, string logDb, string cacheDb)
        {
            return string.Join(Environment.NewLine, new[]
            {
                $"Readarr__Postgres__Host={host}",
                $"Readarr__Postgres__Port={port}",
                "Readarr__Postgres__User=readarr",
                "Readarr__Postgres__Password=<store in Azure Key Vault or deployment secret>",
                $"Readarr__Postgres__MainDb={ValueOrDefault(mainDb, "readarr-main")}",
                $"Readarr__Postgres__LogDb={ValueOrDefault(logDb, "readarr-log")}",
                $"Readarr__Postgres__CacheDb={ValueOrDefault(cacheDb, "readarr-cache")}"
            });
        }

        private static string GetConfigXmlExample(string host, int port, string mainDb, string logDb, string cacheDb)
        {
            return string.Join(Environment.NewLine, new[]
            {
                "<PostgresHost>" + host + "</PostgresHost>",
                "<PostgresPort>" + port + "</PostgresPort>",
                "<PostgresUser>readarr</PostgresUser>",
                "<PostgresPassword>&lt;secret value outside chat/docs&gt;</PostgresPassword>",
                "<PostgresMainDb>" + ValueOrDefault(mainDb, "readarr-main") + "</PostgresMainDb>",
                "<PostgresLogDb>" + ValueOrDefault(logDb, "readarr-log") + "</PostgresLogDb>",
                "<PostgresCacheDb>" + ValueOrDefault(cacheDb, "readarr-cache") + "</PostgresCacheDb>"
            });
        }

        private static string ValueOrDefault(string value, string defaultValue)
        {
            return value.IsNotNullOrWhiteSpace() ? value : defaultValue;
        }

        private static string RedactName(string value)
        {
            return value.IsNotNullOrWhiteSpace() ? value : "database-not-set";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }

            var value = bytes / 1024.0;
            var units = new[] { "KiB", "MiB", "GiB", "TiB" };
            var unit = 0;

            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return $"{value:0.##} {units[unit]}";
        }
    }

    public static class MetadataServiceStatusResourceMapper
    {
        public static MetadataServiceStatusResource ToResource(string configuredMetadataSource,
                                                               MetadataSourceHealthResult healthResult,
                                                               UpdatePackage latestUpdate,
                                                               string updateCheckMessage,
                                                               bool updateCheckSucceeded,
                                                               string branch,
                                                               Version currentVersion,
                                                               DateTime? statusCheckedAt = null)
        {
            var metadataSource = configuredMetadataSource.IsNullOrWhiteSpace() ? MetadataSourceConfig.LocalRReadingGlasses : configuredMetadataSource;
            var redactedMetadataSource = RedactUrl(metadataSource);
            var sourceType = GetSourceType(metadataSource);
            var warnings = new List<string>();
            var readinessState = healthResult.IsHealthy ? "reachable" : "blocked";
            var readinessLabel = healthResult.IsHealthy ? "Metadata service reachable" : "Metadata service unreachable";
            var confidenceSignals = GetConfidenceSignals(sourceType, healthResult.IsHealthy);
            var sidecarStatus = GetSidecarStatus(sourceType, healthResult.ServiceVersion, healthResult.ServiceVersionDetail);

            if (sourceType == "originalReadarr")
            {
                warnings.Add("Original Readarr metadata is legacy compatibility mode. Prefer a ReadAIrr-compatible rreading-glasses endpoint.");
            }

            if (!healthResult.IsHealthy && healthResult.Message.IsNotNullOrWhiteSpace())
            {
                warnings.Add(healthResult.Message);
            }

            if (latestUpdate != null && latestUpdate.Version > currentVersion)
            {
                warnings.Add($"ReadAIrr update {latestUpdate.Version} is available for branch {latestUpdate.Branch ?? branch}.");
            }

            return new MetadataServiceStatusResource
            {
                MetadataSource = metadataSource,
                SourceLabel = GetSourceLabel(sourceType),
                SourceType = sourceType,
                ServiceUrl = redactedMetadataSource,
                ReadinessState = readinessState,
                ReadinessLabel = readinessLabel,
                IsReachable = healthResult.IsHealthy,
                HealthMessage = healthResult.Message,
                HealthDetail = RedactText(healthResult.Detail, metadataSource, redactedMetadataSource),
                StatusCode = healthResult.StatusCode,
                ResponseTimeMs = healthResult.ResponseTimeMs,
                StatusCheckedAt = statusCheckedAt ?? DateTime.UtcNow,
                UpdateEndpoint = "https://readairr.com/v1/update/{branch}",
                UpdateBranch = branch,
                CurrentVersion = currentVersion.ToString(),
                UpdateAvailable = updateCheckSucceeded ? latestUpdate != null && latestUpdate.Version > currentVersion : null,
                LatestVersion = latestUpdate?.Version?.ToString(),
                LatestReleaseDate = latestUpdate?.ReleaseDate,
                UpdateCheckMessage = updateCheckMessage,
                SidecarManagementMode = sidecarStatus.ManagementMode,
                SidecarManagementLabel = sidecarStatus.ManagementLabel,
                SidecarManagedByReadAIrr = sidecarStatus.ManagedByReadAIrr,
                SidecarUpdateSupported = sidecarStatus.UpdateSupported,
                SidecarUpdateAction = sidecarStatus.UpdateAction,
                SidecarCurrentVersion = sidecarStatus.CurrentVersion,
                SidecarLatestVersion = sidecarStatus.LatestVersion,
                SidecarUpdateAvailable = sidecarStatus.UpdateAvailable,
                SidecarVersionMessage = sidecarStatus.VersionMessage,
                SidecarUpdateCheckMessage = sidecarStatus.UpdateCheckMessage,
                SidecarUpdateGuidance = sidecarStatus.UpdateGuidance,
                ConfidenceMode = "sourceRolesOnly",
                ConfidenceSummary = "Confidence foundation is recording source roles only. Automatic metadata decisioning is disabled, so matching/import behavior is unchanged.",
                AutomaticMetadataDecisioningEnabled = false,
                ConfidenceSignals = confidenceSignals,
                Warnings = warnings,
                Checklist = new List<string>
                {
                    "Keep the configured metadata source reachable from the ReadAIrr container.",
                    "Use a ReadAIrr-compatible rreading-glasses endpoint for metadata lookups.",
                    "Use ReadAIrr-owned update metadata before upgrading the app.",
                    sidecarStatus.UpdateGuidance,
                    "Treat confidence signals as audit evidence only until an explicit metadata policy enables cross-source decisions.",
                    "Upgrade metadata services manually; this page never restarts or mutates services."
                }
            };
        }

        private static SidecarStatus GetSidecarStatus(string sourceType, string currentVersion, string versionDetail)
        {
            switch (sourceType)
            {
                case "localRReadingGlasses":
                    return new SidecarStatus
                    {
                        ManagementMode = "readarrDeploymentSidecar",
                        ManagementLabel = "ReadAIrr deployment sidecar",
                        ManagedByReadAIrr = true,
                        UpdateSupported = false,
                        UpdateAction = "manualDockerImageUpdate",
                        CurrentVersion = currentVersion,
                        LatestVersion = null,
                        UpdateAvailable = null,
                        VersionMessage = versionDetail.IsNotNullOrWhiteSpace() ? versionDetail : "The configured rreading-glasses endpoint does not expose version metadata to ReadAIrr yet.",
                        UpdateCheckMessage = "ReadAIrr can refresh sidecar reachability and version metadata here, but does not mutate or restart the sidecar.",
                        UpdateGuidance = "For Docker deployments, update rreading-glasses by pulling the newer sidecar image and restarting the compose/deployment outside ReadAIrr."
                    };
                case "hostedGoodreads":
                case "hostedHardcover":
                    return new SidecarStatus
                    {
                        ManagementMode = "externalHosted",
                        ManagementLabel = "Externally managed hosted service",
                        ManagedByReadAIrr = false,
                        UpdateSupported = false,
                        UpdateAction = "managedExternally",
                        CurrentVersion = null,
                        LatestVersion = null,
                        UpdateAvailable = null,
                        VersionMessage = "Hosted rreading-glasses services do not expose version metadata to this ReadAIrr instance.",
                        UpdateCheckMessage = "Hosted metadata service updates are managed outside ReadAIrr.",
                        UpdateGuidance = "ReadAIrr can report hosted service reachability, but hosted rreading-glasses updates are controlled by the service operator."
                    };
                case "originalReadarr":
                    return new SidecarStatus
                    {
                        ManagementMode = "legacyCompatibility",
                        ManagementLabel = "Legacy compatibility metadata",
                        ManagedByReadAIrr = false,
                        UpdateSupported = false,
                        UpdateAction = "switchMetadataSource",
                        CurrentVersion = null,
                        LatestVersion = null,
                        UpdateAvailable = null,
                        VersionMessage = "Original Readarr metadata compatibility is not a rreading-glasses sidecar.",
                        UpdateCheckMessage = "No sidecar update check applies while using original Readarr compatibility metadata.",
                        UpdateGuidance = "Switch to a ReadAIrr-compatible rreading-glasses endpoint before using sidecar update guidance."
                    };
                default:
                    return new SidecarStatus
                    {
                        ManagementMode = "externalCustom",
                        ManagementLabel = "Custom external metadata service",
                        ManagedByReadAIrr = false,
                        UpdateSupported = false,
                        UpdateAction = "managedExternally",
                        CurrentVersion = null,
                        LatestVersion = null,
                        UpdateAvailable = null,
                        VersionMessage = "Custom metadata services do not expose version metadata through this status surface.",
                        UpdateCheckMessage = "Custom metadata service updates are managed outside ReadAIrr.",
                        UpdateGuidance = "Update custom metadata services using their own deployment process; ReadAIrr only checks reachability."
                    };
            }
        }

        private static List<MetadataSourceConfidenceSignalResource> GetConfidenceSignals(string activeSourceType, bool activeSourceHealthy)
        {
            var signals = new List<MetadataSourceConfidenceSignalResource>
            {
                ToConfidenceSignal(activeSourceType,
                    "primary",
                    true,
                    activeSourceHealthy,
                    activeSourceHealthy ? 100 : 0,
                    activeSourceHealthy ? "reachable" : "unreachable",
                    "Current configured metadata source. This remains the only source used by normal metadata lookup, matching, and import decisions.")
            };

            AddReferenceSignal(signals, activeSourceType, "hostedGoodreads");
            AddReferenceSignal(signals, activeSourceType, "hostedHardcover");

            signals.Add(new MetadataSourceConfidenceSignalResource
            {
                SourceType = "aiReview",
                SourceLabel = "Optional AI review",
                Role = "futureManualReview",
                IsActive = false,
                IsAvailable = false,
                ConfidenceWeight = 0,
                Status = "disabled",
                Explanation = "Reserved for future opt-in review suggestions. This stub never allows AI/STT evidence to auto-import or override metadata."
            });

            return signals;
        }

        private static void AddReferenceSignal(List<MetadataSourceConfidenceSignalResource> signals, string activeSourceType, string sourceType)
        {
            if (activeSourceType == sourceType)
            {
                return;
            }

            signals.Add(ToConfidenceSignal(sourceType,
                "futureReference",
                false,
                false,
                0,
                "notEvaluated",
                "Known ReadAIrr-compatible metadata source reserved for future cross-source confidence checks. It is not queried by this stub."));
        }

        private static MetadataSourceConfidenceSignalResource ToConfidenceSignal(string sourceType,
                                                                                string role,
                                                                                bool isActive,
                                                                                bool isAvailable,
                                                                                int confidenceWeight,
                                                                                string status,
                                                                                string explanation)
        {
            return new MetadataSourceConfidenceSignalResource
            {
                SourceType = sourceType,
                SourceLabel = GetSourceLabel(sourceType),
                Role = role,
                IsActive = isActive,
                IsAvailable = isAvailable,
                ConfidenceWeight = confidenceWeight,
                Status = status,
                Explanation = explanation
            };
        }

        private static string GetSourceType(string metadataSource)
        {
            if (MetadataSourceConfig.IsOriginalReadarr(metadataSource))
            {
                return "originalReadarr";
            }

            if (metadataSource.Equals(MetadataSourceConfig.LocalRReadingGlasses))
            {
                return "localRReadingGlasses";
            }

            if (metadataSource.Equals(MetadataSourceConfig.GoodreadsHosted))
            {
                return "hostedGoodreads";
            }

            if (metadataSource.Equals(MetadataSourceConfig.HardcoverHosted))
            {
                return "hostedHardcover";
            }

            return "custom";
        }

        private static string GetSourceLabel(string sourceType)
        {
            switch (sourceType)
            {
                case "localRReadingGlasses":
                    return "Automatic self-hosted rreading-glasses";
                case "hostedGoodreads":
                    return "rreading-glasses Goodreads hosted";
                case "hostedHardcover":
                    return "rreading-glasses Hardcover hosted";
                case "originalReadarr":
                    return "Original Readarr metadata compatibility";
                case "aiReview":
                    return "Optional AI review";
                default:
                    return "Custom metadata service";
            }
        }

        private static string RedactUrl(string value)
        {
            if (MetadataSourceConfig.IsOriginalReadarr(value) || value.IsNullOrWhiteSpace())
            {
                return value;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                return value;
            }

            var builder = new UriBuilder(uri)
            {
                UserName = uri.UserInfo.IsNullOrWhiteSpace() ? string.Empty : "redacted",
                Password = string.Empty,
                Query = string.Empty
            };

            return builder.Uri.ToString().TrimEnd('/');
        }

        private static string RedactText(string value, string rawSource, string redactedSource)
        {
            if (value.IsNullOrWhiteSpace() || rawSource.IsNullOrWhiteSpace())
            {
                return value;
            }

            return value.Replace(rawSource, redactedSource);
        }

        private class SidecarStatus
        {
            public string ManagementMode { get; set; }
            public string ManagementLabel { get; set; }
            public bool ManagedByReadAIrr { get; set; }
            public bool UpdateSupported { get; set; }
            public string UpdateAction { get; set; }
            public string CurrentVersion { get; set; }
            public string LatestVersion { get; set; }
            public bool? UpdateAvailable { get; set; }
            public string VersionMessage { get; set; }
            public string UpdateCheckMessage { get; set; }
            public string UpdateGuidance { get; set; }
        }
    }
}
