using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Backup
{
    public interface IBackupService
    {
        void Backup(BackupType backupType);
        List<Backup> GetBackups();
        void Restore(string backupFileName);
        void RestoreWithMigrationDecisions(string backupFileName, Dictionary<string, string> userDecisions);
        List<ConfigurationConflict> AnalyzeBackupConflicts(string backupFileName);
        string GetBackupFolder();
        string GetBackupFolder(BackupType backupType);
    }

    public class BackupService : IBackupService, IExecute<BackupCommand>
    {
        private readonly IMainDatabase _maindDb;
        private readonly IMakeDatabaseBackup _makeDatabaseBackup;
        private readonly IDiskTransferService _diskTransferService;
        private readonly IDiskProvider _diskProvider;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IArchiveService _archiveService;
        private readonly IConfigService _configService;
        private readonly IBackupMigrationService _migrationService;
        private readonly Logger _logger;

        private string _backupTempFolder;

        public static readonly Regex BackupFileRegex = new Regex(@"readairr_backup_(v[0-9.]+_)?[._0-9]+\.zip", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public BackupService(IMainDatabase maindDb,
                             IMakeDatabaseBackup makeDatabaseBackup,
                             IDiskTransferService diskTransferService,
                             IDiskProvider diskProvider,
                             IAppFolderInfo appFolderInfo,
                             IArchiveService archiveService,
                             IConfigService configService,
                             IBackupMigrationService migrationService,
                             Logger logger)
        {
            _maindDb = maindDb;
            _makeDatabaseBackup = makeDatabaseBackup;
            _diskTransferService = diskTransferService;
            _diskProvider = diskProvider;
            _appFolderInfo = appFolderInfo;
            _archiveService = archiveService;
            _configService = configService;
            _migrationService = migrationService;
            _logger = logger;

            _backupTempFolder = Path.Combine(_appFolderInfo.TempFolder, "readairr_backup");
        }

        public void Backup(BackupType backupType)
        {
            _logger.ProgressInfo("Starting Backup");

            var backupFolder = GetBackupFolder(backupType);

            _diskProvider.EnsureFolder(_backupTempFolder);
            _diskProvider.EnsureFolder(backupFolder);

            if (!_diskProvider.FolderWritable(backupFolder))
            {
                throw new UnauthorizedAccessException($"Backup folder {backupFolder} is not writable");
            }

            var dateNow = DateTime.Now;
            var backupFilename = $"readairr_backup_v{BuildInfo.Version}_{dateNow:yyyy.MM.dd_HH.mm.ss}.zip";
            var backupPath = Path.Combine(backupFolder, backupFilename);

            Cleanup();

            if (backupType != BackupType.Manual)
            {
                CleanupOldBackups(backupType);
            }

            BackupConfigFile();
            BackupDatabase();
            CreateVersionInfo(dateNow);

            _logger.ProgressDebug("Creating backup zip");

            // Delete journal file created during database backup
            _diskProvider.DeleteFile(Path.Combine(_backupTempFolder, "readarr.db-journal"));

            _archiveService.CreateZip(backupPath, _diskProvider.GetFiles(_backupTempFolder, false));

            Cleanup();

            _logger.ProgressDebug("Backup zip created");
        }

        public List<Backup> GetBackups()
        {
            var backups = new List<Backup>();

            foreach (var backupType in Enum.GetValues(typeof(BackupType)).Cast<BackupType>())
            {
                var folder = GetBackupFolder(backupType);

                if (_diskProvider.FolderExists(folder))
                {
                    backups.AddRange(GetBackupFiles(folder).Select(b => new Backup
                    {
                        Name = Path.GetFileName(b),
                        Type = backupType,
                        Size = _diskProvider.GetFileSize(b),
                        Time = _diskProvider.FileGetLastWrite(b)
                    }));
                }
            }

            return backups;
        }

        public void Restore(string backupFileName)
        {
            if (backupFileName.EndsWith(".zip"))
            {
                var restoredFile = false;
                var temporaryPath = Path.Combine(_appFolderInfo.TempFolder, "readairr_backup_restore");

                _archiveService.Extract(backupFileName, temporaryPath);

                string configFileToRestore = null;
                string databaseFileToRestore = null;

                // Find files to restore
                foreach (var file in _diskProvider.GetFiles(temporaryPath, false))
                {
                    var fileName = Path.GetFileName(file);

                    if (fileName.Equals("Config.xml", StringComparison.InvariantCultureIgnoreCase))
                    {
                        configFileToRestore = file;
                    }

                    if (fileName.Equals("readarr.db", StringComparison.InvariantCultureIgnoreCase))
                    {
                        databaseFileToRestore = file;
                    }
                }

                if (configFileToRestore != null)
                {
                    var currentConfigPath = _appFolderInfo.GetConfigPath();

                    try
                    {
                        // Analyze and migrate the config if needed
                        _migrationService.AnalyzeAndMigrateBackupConfig(currentConfigPath, configFileToRestore);

                        // If we get here, no conflicts were found or they were resolved
                        _diskProvider.MoveFile(configFileToRestore, currentConfigPath, true);
                        restoredFile = true;
                        _logger.Info("Successfully restored config file");
                    }
                    catch (BackupMigrationConflictException)
                    {
                        // Re-throw migration conflicts so the API can handle them
                        _diskProvider.DeleteFolder(temporaryPath, true);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error during config migration, falling back to direct restore");

                        // Fall back to direct restore if migration fails
                        _diskProvider.MoveFile(configFileToRestore, currentConfigPath, true);
                        restoredFile = true;
                    }
                }

                // Handle database restoration
                if (databaseFileToRestore != null)
                {
                    _diskProvider.MoveFile(databaseFileToRestore, _appFolderInfo.GetDatabaseRestore(), true);
                    restoredFile = true;
                }

                if (!restoredFile)
                {
                    throw new RestoreBackupFailedException(HttpStatusCode.NotFound, "Unable to restore database file from backup");
                }

                _diskProvider.DeleteFolder(temporaryPath, true);

                return;
            }

            _diskProvider.MoveFile(backupFileName, _appFolderInfo.GetDatabaseRestore(), true);
        }

        public void RestoreWithMigrationDecisions(string backupFileName, Dictionary<string, string> userDecisions)
        {
            if (backupFileName.EndsWith(".zip"))
            {
                var restoredFile = false;
                var temporaryPath = Path.Combine(_appFolderInfo.TempFolder, "readairr_backup_restore");

                _archiveService.Extract(backupFileName, temporaryPath);

                string configFileToRestore = null;
                string databaseFileToRestore = null;

                // Find files to restore
                foreach (var file in _diskProvider.GetFiles(temporaryPath, false))
                {
                    var fileName = Path.GetFileName(file);

                    if (fileName.Equals("Config.xml", StringComparison.InvariantCultureIgnoreCase))
                    {
                        configFileToRestore = file;
                    }

                    if (fileName.Equals("readarr.db", StringComparison.InvariantCultureIgnoreCase))
                    {
                        databaseFileToRestore = file;
                    }
                }

                if (configFileToRestore != null)
                {
                    var currentConfigPath = _appFolderInfo.GetConfigPath();

                    try
                    {
                        // Apply user decisions during migration
                        _migrationService.AnalyzeAndMigrateBackupConfig(currentConfigPath, configFileToRestore, false, userDecisions);

                        _diskProvider.MoveFile(configFileToRestore, currentConfigPath, true);
                        restoredFile = true;
                        _logger.Info("Successfully restored config file with user migration decisions");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error during config migration with user decisions");
                        _diskProvider.DeleteFolder(temporaryPath, true);
                        throw;
                    }
                }

                // Handle database restoration
                if (databaseFileToRestore != null)
                {
                    _diskProvider.MoveFile(databaseFileToRestore, _appFolderInfo.GetDatabaseRestore(), true);
                    restoredFile = true;
                }

                if (!restoredFile)
                {
                    throw new RestoreBackupFailedException(HttpStatusCode.NotFound, "Unable to restore database file from backup");
                }

                _diskProvider.DeleteFolder(temporaryPath, true);

                return;
            }

            _diskProvider.MoveFile(backupFileName, _appFolderInfo.GetDatabaseRestore(), true);
        }

        public List<ConfigurationConflict> AnalyzeBackupConflicts(string backupFileName)
        {
            if (!backupFileName.EndsWith(".zip"))
            {
                return new List<ConfigurationConflict>();
            }

            var temporaryPath = Path.Combine(_appFolderInfo.TempFolder, "readairr_backup_analyze");

            try
            {
                _archiveService.Extract(backupFileName, temporaryPath);

                string configFileToAnalyze = null;

                foreach (var file in _diskProvider.GetFiles(temporaryPath, false))
                {
                    var fileName = Path.GetFileName(file);

                    if (fileName.Equals("Config.xml", StringComparison.InvariantCultureIgnoreCase))
                    {
                        configFileToAnalyze = file;
                        break;
                    }
                }

                if (configFileToAnalyze == null)
                {
                    return new List<ConfigurationConflict>();
                }

                var currentConfigPath = _appFolderInfo.GetConfigPath();
                return _migrationService.AnalyzeConfigConflicts(currentConfigPath, configFileToAnalyze);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error analyzing backup conflicts for {0}", backupFileName);
                throw;
            }
            finally
            {
                if (_diskProvider.FolderExists(temporaryPath))
                {
                    _diskProvider.DeleteFolder(temporaryPath, true);
                }
            }
        }

        public string GetBackupFolder()
        {
            var backupFolder = _configService.BackupFolder;

            if (Path.IsPathRooted(backupFolder))
            {
                return backupFolder;
            }

            return Path.Combine(_appFolderInfo.GetAppDataPath(), backupFolder);
        }

        public string GetBackupFolder(BackupType backupType)
        {
            return Path.Combine(GetBackupFolder(), backupType.ToString().ToLower());
        }

        private void Cleanup()
        {
            if (_diskProvider.FolderExists(_backupTempFolder))
            {
                _diskProvider.EmptyFolder(_backupTempFolder);
            }
        }

        private void BackupDatabase()
        {
            if (_maindDb.DatabaseType == DatabaseType.SQLite)
            {
                _logger.ProgressDebug("Backing up database");

                _makeDatabaseBackup.BackupDatabase(_maindDb, _backupTempFolder);
            }
        }

        private void BackupConfigFile()
        {
            _logger.ProgressDebug("Backing up config.xml");

            var configFile = _appFolderInfo.GetConfigPath();
            var tempConfigFile = Path.Combine(_backupTempFolder, Path.GetFileName(configFile));

            _diskTransferService.TransferFile(configFile, tempConfigFile, TransferMode.Copy);
        }

        private void CreateVersionInfo(DateTime dateNow)
        {
            var tempFile = Path.Combine(_backupTempFolder, "INFO");

            var builder = new StringBuilder();
            builder.AppendLine($"v{BuildInfo.Version}");
            builder.AppendLine($"{dateNow:yyyy-MM-dd HH:mm:ss}");

            _diskProvider.WriteAllText(tempFile, builder.ToString());
        }

        private void CleanupOldBackups(BackupType backupType)
        {
            var retention = _configService.BackupRetention;

            _logger.Debug("Cleaning up backup files older than {0} days", retention);
            var files = GetBackupFiles(GetBackupFolder(backupType));

            foreach (var file in files)
            {
                var lastWriteTime = _diskProvider.FileGetLastWrite(file);

                if (lastWriteTime.AddDays(retention) < DateTime.UtcNow)
                {
                    _logger.Debug("Deleting old backup file: {0}", file);
                    _diskProvider.DeleteFile(file);
                }
            }

            _logger.Debug("Finished cleaning up old backup files");
        }

        private IEnumerable<string> GetBackupFiles(string path)
        {
            var files = _diskProvider.GetFiles(path, false);

            return files.Where(f => BackupFileRegex.IsMatch(f));
        }

        public void Execute(BackupCommand message)
        {
            Backup(message.Type);
        }
    }
}
