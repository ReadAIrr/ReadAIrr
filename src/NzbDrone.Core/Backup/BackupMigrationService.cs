using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.Backup
{
    public interface IBackupMigrationService
    {
        void AnalyzeAndMigrateBackupConfig(string configPath, string backupConfigPath, bool forceOverride = false, Dictionary<string, string> userDecisions = null);
        List<ConfigurationConflict> AnalyzeConfigConflicts(string currentConfigPath, string backupConfigPath);
        bool IsLegacyReadarrBackup(string configPath);
    }

    public class BackupMigrationService : IBackupMigrationService
    {
        private const string CONFIG_ELEMENT_NAME = "Config";

        // Legacy Readarr default ports
        private const int LEGACY_READARR_DEFAULT_PORT = 8787;
        private const int LEGACY_READARR_DEFAULT_SSL_PORT = 6868;

        // ReadAIrr default ports
        private const int READAIRR_DEFAULT_PORT = 8246;
        private const int READAIRR_DEFAULT_SSL_PORT = 8247;

        private readonly IConfigFileProvider _configFileProvider;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public BackupMigrationService(IConfigFileProvider configFileProvider, IDiskProvider diskProvider, Logger logger)
        {
            _configFileProvider = configFileProvider;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public bool IsLegacyReadarrBackup(string configPath)
        {
            if (!_diskProvider.FileExists(configPath))
            {
                return false;
            }

            try
            {
                var doc = XDocument.Load(configPath);
                var config = doc.Descendants(CONFIG_ELEMENT_NAME).FirstOrDefault();

                if (config == null)
                {
                    return false;
                }

                // Check for legacy port configurations
                var port = GetConfigValue(config, "Port");
                var sslPort = GetConfigValue(config, "SslPort");

                // If either port matches legacy defaults, consider it legacy
                if ((port != null && int.TryParse(port, out var portInt) && portInt == LEGACY_READARR_DEFAULT_PORT) ||
                    (sslPort != null && int.TryParse(sslPort, out var sslPortInt) && sslPortInt == LEGACY_READARR_DEFAULT_SSL_PORT))
                {
                    return true;
                }

                // Check for application name indicators
                var instanceName = GetConfigValue(config, "InstanceName");
                if (instanceName != null && instanceName.ToLower().Contains("readarr") && !instanceName.ToLower().Contains("readairr"))
                {
                    return true;
                }

                // Check for any URLs or references that might indicate legacy Readarr
                var urlBase = GetConfigValue(config, "UrlBase");
                if (urlBase != null && urlBase.ToLower().Contains("readarr") && !urlBase.ToLower().Contains("readairr"))
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Error analyzing config file for legacy Readarr detection: {0}", configPath);
                return false;
            }
        }

        public List<ConfigurationConflict> AnalyzeConfigConflicts(string currentConfigPath, string backupConfigPath)
        {
            var conflicts = new List<ConfigurationConflict>();

            if (!_diskProvider.FileExists(backupConfigPath))
            {
                return conflicts;
            }

            try
            {
                // Load current config (or get defaults if it doesn't exist)
                XDocument currentDoc = null;
                XElement currentConfig = null;

                if (_diskProvider.FileExists(currentConfigPath))
                {
                    currentDoc = XDocument.Load(currentConfigPath);
                    currentConfig = currentDoc.Descendants(CONFIG_ELEMENT_NAME).FirstOrDefault();
                }

                // Load backup config
                var backupDoc = XDocument.Load(backupConfigPath);
                var backupConfig = backupDoc.Descendants(CONFIG_ELEMENT_NAME).FirstOrDefault();

                if (backupConfig == null)
                {
                    return conflicts;
                }

                // Check port configurations
                CheckPortConflicts(currentConfig, backupConfig, conflicts);

                // Check instance name conflicts
                CheckInstanceNameConflicts(currentConfig, backupConfig, conflicts);

                // Check URL base conflicts
                CheckUrlBaseConflicts(currentConfig, backupConfig, conflicts);

                return conflicts;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error analyzing configuration conflicts between {0} and {1}", currentConfigPath, backupConfigPath);
                throw;
            }
        }

        public void AnalyzeAndMigrateBackupConfig(string configPath, string backupConfigPath, bool forceOverride = false, Dictionary<string, string> userDecisions = null)
        {
            var conflicts = AnalyzeConfigConflicts(configPath, backupConfigPath);
            var isLegacy = IsLegacyReadarrBackup(backupConfigPath);

            _logger.Info("Analyzing backup config: {0} conflicts found, Legacy Readarr: {1}", conflicts.Count, isLegacy);

            // If no conflicts, proceed normally
            if (!conflicts.Any() && !forceOverride)
            {
                _logger.Info("No configuration conflicts detected, proceeding with standard restore");
                return;
            }

            // If we have conflicts and no user decisions, throw exception for UI handling
            if (conflicts.Any() && userDecisions == null && !forceOverride)
            {
                _logger.Info("Configuration conflicts detected, requiring user intervention");
                throw new BackupMigrationConflictException(conflicts, isLegacy);
            }

            // Apply migrations based on user decisions or defaults
            ApplyConfigMigration(configPath, backupConfigPath, conflicts, userDecisions, isLegacy);
        }

        private void CheckPortConflicts(XElement currentConfig, XElement backupConfig, List<ConfigurationConflict> conflicts)
        {
            // Check HTTP Port
            var currentPort = GetConfigValue(currentConfig, "Port") ?? READAIRR_DEFAULT_PORT.ToString();
            var backupPort = GetConfigValue(backupConfig, "Port");

            if (backupPort != null && backupPort != currentPort)
            {
                var conflict = new ConfigurationConflict
                {
                    Setting = "Port",
                    CurrentValue = currentPort,
                    BackupValue = backupPort,
                    Type = ConflictType.PortConfiguration,
                    Description = "HTTP port configuration differs between current installation and backup",
                    SuggestedActions = new List<string>
                    {
                        $"Keep current ReadAIrr port ({currentPort})",
                        $"Use backup port ({backupPort})",
                        $"Use ReadAIrr default port ({READAIRR_DEFAULT_PORT})"
                    }
                };

                // Add special suggestion for legacy Readarr ports
                if (int.TryParse(backupPort, out var portInt) && portInt == LEGACY_READARR_DEFAULT_PORT)
                {
                    conflict.SuggestedActions.Insert(0, $"Migrate from legacy Readarr port {LEGACY_READARR_DEFAULT_PORT} to ReadAIrr port {READAIRR_DEFAULT_PORT} (Recommended)");
                }

                conflicts.Add(conflict);
            }

            // Check SSL Port
            var currentSslPort = GetConfigValue(currentConfig, "SslPort") ?? READAIRR_DEFAULT_SSL_PORT.ToString();
            var backupSslPort = GetConfigValue(backupConfig, "SslPort");

            if (backupSslPort != null && backupSslPort != currentSslPort)
            {
                var conflict = new ConfigurationConflict
                {
                    Setting = "SslPort",
                    CurrentValue = currentSslPort,
                    BackupValue = backupSslPort,
                    Type = ConflictType.PortConfiguration,
                    Description = "SSL port configuration differs between current installation and backup",
                    SuggestedActions = new List<string>
                    {
                        $"Keep current ReadAIrr SSL port ({currentSslPort})",
                        $"Use backup SSL port ({backupSslPort})",
                        $"Use ReadAIrr default SSL port ({READAIRR_DEFAULT_SSL_PORT})"
                    }
                };

                // Add special suggestion for legacy Readarr SSL ports
                if (int.TryParse(backupSslPort, out var sslPortInt) && sslPortInt == LEGACY_READARR_DEFAULT_SSL_PORT)
                {
                    conflict.SuggestedActions.Insert(0, $"Migrate from legacy Readarr SSL port {LEGACY_READARR_DEFAULT_SSL_PORT} to ReadAIrr SSL port {READAIRR_DEFAULT_SSL_PORT} (Recommended)");
                }

                conflicts.Add(conflict);
            }
        }

        private void CheckInstanceNameConflicts(XElement currentConfig, XElement backupConfig, List<ConfigurationConflict> conflicts)
        {
            var currentInstanceName = GetConfigValue(currentConfig, "InstanceName") ?? "ReadAIrr";
            var backupInstanceName = GetConfigValue(backupConfig, "InstanceName");

            if (backupInstanceName != null && backupInstanceName.ToLower().Contains("readarr") && !backupInstanceName.ToLower().Contains("readairr"))
            {
                conflicts.Add(new ConfigurationConflict
                {
                    Setting = "InstanceName",
                    CurrentValue = currentInstanceName,
                    BackupValue = backupInstanceName,
                    Type = ConflictType.ApplicationName,
                    Description = "Instance name appears to be from legacy Readarr",
                    SuggestedActions = new List<string>
                    {
                        $"Use ReadAIrr instance name ('{currentInstanceName}')",
                        $"Keep backup instance name ('{backupInstanceName}')",
                        $"Create new instance name based on backup ('ReadAIrr-{backupInstanceName.Replace("Readarr", "").Trim()}')"
                    }
                });
            }
        }

        private void CheckUrlBaseConflicts(XElement currentConfig, XElement backupConfig, List<ConfigurationConflict> conflicts)
        {
            var currentUrlBase = GetConfigValue(currentConfig, "UrlBase") ?? "";
            var backupUrlBase = GetConfigValue(backupConfig, "UrlBase");

            if (backupUrlBase != null && backupUrlBase != currentUrlBase)
            {
                conflicts.Add(new ConfigurationConflict
                {
                    Setting = "UrlBase",
                    CurrentValue = currentUrlBase,
                    BackupValue = backupUrlBase,
                    Type = ConflictType.PathConfiguration,
                    Description = "URL base path differs between current installation and backup",
                    SuggestedActions = new List<string>
                    {
                        $"Keep current URL base ('{currentUrlBase}')",
                        $"Use backup URL base ('{backupUrlBase}')"
                    }
                });
            }
        }

        private void ApplyConfigMigration(string configPath, string backupConfigPath, List<ConfigurationConflict> conflicts, Dictionary<string, string> userDecisions, bool isLegacy)
        {
            _logger.Info("Applying configuration migration with {0} user decisions", userDecisions?.Count ?? 0);

            try
            {
                // Load backup config
                var backupDoc = XDocument.Load(backupConfigPath);
                var backupConfig = backupDoc.Descendants(CONFIG_ELEMENT_NAME).FirstOrDefault();

                if (backupConfig == null)
                {
                    _logger.Warn("No config element found in backup file, proceeding with standard copy");
                    return;
                }

                // Apply user decisions or intelligent defaults
                foreach (var conflict in conflicts)
                {
                    string decision = null;
                    userDecisions?.TryGetValue(conflict.Setting, out decision);

                    switch (conflict.Setting)
                    {
                        case "Port":
                            ApplyPortDecision(backupConfig, conflict, decision, isLegacy, READAIRR_DEFAULT_PORT);
                            break;
                        case "SslPort":
                            ApplyPortDecision(backupConfig, conflict, decision, isLegacy, READAIRR_DEFAULT_SSL_PORT);
                            break;
                        case "InstanceName":
                            ApplyInstanceNameDecision(backupConfig, conflict, decision);
                            break;
                        case "UrlBase":
                            ApplyUrlBaseDecision(backupConfig, conflict, decision);
                            break;
                    }
                }

                // Save the modified backup config
                _diskProvider.WriteAllText(backupConfigPath, backupDoc.ToString());
                _logger.Info("Successfully applied configuration migration to backup file");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error applying configuration migration");
                throw;
            }
        }

        private void ApplyPortDecision(XElement config, ConfigurationConflict conflict, string decision, bool isLegacy, int defaultPort)
        {
            var newValue = decision switch
            {
                "keep_current" => conflict.CurrentValue,
                "use_backup" => conflict.BackupValue,
                "use_default" => defaultPort.ToString(),
                "migrate_legacy" when isLegacy => defaultPort.ToString(),
                _ when isLegacy => defaultPort.ToString(), // Default for legacy: migrate to new ports
                _ => conflict.CurrentValue // Default for non-legacy: keep current
            };

            SetConfigValue(config, conflict.Setting, newValue);
            _logger.Info("Applied port migration for {0}: {1} -> {2}", conflict.Setting, conflict.BackupValue, newValue);
        }

        private void ApplyInstanceNameDecision(XElement config, ConfigurationConflict conflict, string decision)
        {
            var newValue = decision switch
            {
                "keep_current" => conflict.CurrentValue,
                "use_backup" => conflict.BackupValue,
                "create_new" => $"ReadAIrr-{conflict.BackupValue.Replace("Readarr", "").Trim()}",
                _ => conflict.CurrentValue // Default: keep current
            };

            SetConfigValue(config, conflict.Setting, newValue);
            _logger.Info("Applied instance name migration: {0} -> {1}", conflict.BackupValue, newValue);
        }

        private void ApplyUrlBaseDecision(XElement config, ConfigurationConflict conflict, string decision)
        {
            var newValue = decision switch
            {
                "keep_current" => conflict.CurrentValue,
                "use_backup" => conflict.BackupValue,
                _ => conflict.CurrentValue // Default: keep current
            };

            SetConfigValue(config, conflict.Setting, newValue);
            _logger.Info("Applied URL base migration: {0} -> {1}", conflict.BackupValue, newValue);
        }

        private string GetConfigValue(XElement config, string key)
        {
            if (config == null)
            {
                return null;
            }

            var element = config.Descendants(key).FirstOrDefault();
            return element?.Value?.Trim();
        }

        private void SetConfigValue(XElement config, string key, string value)
        {
            var element = config.Descendants(key).FirstOrDefault();
            if (element != null)
            {
                element.Value = value;
            }
            else
            {
                config.Add(new XElement(key, value));
            }
        }
    }
}
