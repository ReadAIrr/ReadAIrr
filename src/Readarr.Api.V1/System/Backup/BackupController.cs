using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Crypto;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Backup;
using NzbDrone.Http.REST.Attributes;
using Readarr.Http;
using Readarr.Http.REST;

namespace Readarr.Api.V1.System.Backup
{
    [V1ApiController("system/backup")]
    public class BackupController : Controller
    {
        private readonly IBackupService _backupService;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IDiskProvider _diskProvider;

        private static readonly List<string> ValidExtensions = new () { ".zip", ".db", ".xml" };

        public BackupController(IBackupService backupService,
                            IAppFolderInfo appFolderInfo,
                            IDiskProvider diskProvider)
        {
            _backupService = backupService;
            _appFolderInfo = appFolderInfo;
            _diskProvider = diskProvider;
        }

        [HttpGet]
        public List<BackupResource> GetBackupFiles()
        {
            var backups = _backupService.GetBackups();

            return backups.Select(b => new BackupResource
                {
                    Id = GetBackupId(b),
                    Name = b.Name,
                    Path = $"/backup/{b.Type.ToString().ToLower()}/{b.Name}",
                    Type = b.Type,
                    Size = b.Size,
                    Time = b.Time
                })
                .OrderByDescending(b => b.Time)
                .ToList();
        }

        [RestDeleteById]
        public object DeleteBackup(int id)
        {
            var backup = GetBackup(id);

            if (backup == null)
            {
                throw new NotFoundException();
            }

            var path = GetBackupPath(backup);

            if (!_diskProvider.FileExists(path))
            {
                throw new NotFoundException();
            }

            _diskProvider.DeleteFile(path);

            return new { };
        }

        [HttpPost("restore/{id:int}")]
        public object Restore(int id)
        {
            var backup = GetBackup(id);

            if (backup == null)
            {
                throw new NotFoundException();
            }

            var path = GetBackupPath(backup);

            try
            {
                _backupService.Restore(path);

                return new
                {
                    RestartRequired = true
                };
            }
            catch (BackupMigrationConflictException ex)
            {
                return new
                {
                    MigrationRequired = true,
                    IsLegacyReadarrBackup = ex.IsLegacyReadarrBackup,
                    Conflicts = ex.Conflicts.Select(c => new
                    {
                        Setting = c.Setting,
                        CurrentValue = c.CurrentValue,
                        BackupValue = c.BackupValue,
                        Description = c.Description,
                        Type = c.Type.ToString(),
                        SuggestedActions = c.SuggestedActions
                    }).ToList(),
                    Message = ex.Message
                };
            }
        }

        [HttpPost("restore/{id:int}/migrate")]
        public object RestoreWithMigration(int id, [FromBody] MigrationDecisionRequest request)
        {
            var backup = GetBackup(id);

            if (backup == null)
            {
                throw new NotFoundException();
            }

            var path = GetBackupPath(backup);

            _backupService.RestoreWithMigrationDecisions(path, request.Decisions);

            return new
            {
                RestartRequired = true
            };
        }

        [HttpGet("analyze/{id:int}")]
        public object AnalyzeBackup(int id)
        {
            var backup = GetBackup(id);

            if (backup == null)
            {
                throw new NotFoundException();
            }

            var path = GetBackupPath(backup);
            var conflicts = _backupService.AnalyzeBackupConflicts(path);

            return new
            {
                HasConflicts = conflicts.Any(),
                IsLegacyReadarrBackup = conflicts.Any() && _backupService.GetType().Name.Contains("Migration"), // Simple check for now
                Conflicts = conflicts.Select(c => new
                {
                    Setting = c.Setting,
                    CurrentValue = c.CurrentValue,
                    BackupValue = c.BackupValue,
                    Description = c.Description,
                    Type = c.Type.ToString(),
                    SuggestedActions = c.SuggestedActions
                }).ToList()
            };
        }

        [HttpPost("restore/upload")]
        [RequestFormLimits(MultipartBodyLengthLimit = 1000000000)]
        public object UploadAndRestore()
        {
            var files = Request.Form.Files;

            if (files.Empty())
            {
                throw new BadRequestException("file must be provided");
            }

            var file = files.First();
            var extension = Path.GetExtension(file.FileName);

            if (!ValidExtensions.Contains(extension))
            {
                throw new UnsupportedMediaTypeException($"Invalid extension, must be one of: {ValidExtensions.Join(", ")}");
            }

            var path = Path.Combine(_appFolderInfo.TempFolder, $"readairr_backup_restore{extension}");

            _diskProvider.SaveStream(file.OpenReadStream(), path);

            try
            {
                _backupService.Restore(path);

                // Cleanup restored file
                _diskProvider.DeleteFile(path);

                return new
                {
                    RestartRequired = true
                };
            }
            catch (BackupMigrationConflictException ex)
            {
                // Don't delete the temp file yet, we'll need it for the migration step
                return new
                {
                    MigrationRequired = true,
                    IsLegacyReadarrBackup = ex.IsLegacyReadarrBackup,
                    TempFileName = Path.GetFileName(path),
                    Conflicts = ex.Conflicts.Select(c => new
                    {
                        Setting = c.Setting,
                        CurrentValue = c.CurrentValue,
                        BackupValue = c.BackupValue,
                        Description = c.Description,
                        Type = c.Type.ToString(),
                        SuggestedActions = c.SuggestedActions
                    }).ToList(),
                    Message = ex.Message
                };
            }
        }

        [HttpPost("restore/upload/migrate")]
        public object UploadAndRestoreWithMigration([FromBody] UploadMigrationDecisionRequest request)
        {
            var path = Path.Combine(_appFolderInfo.TempFolder, request.TempFileName);

            if (!_diskProvider.FileExists(path))
            {
                throw new NotFoundException("Temporary backup file not found");
            }

            try
            {
                _backupService.RestoreWithMigrationDecisions(path, request.Decisions);

                // Cleanup restored file
                _diskProvider.DeleteFile(path);

                return new
                {
                    RestartRequired = true
                };
            }
            catch (Exception)
            {
                // Cleanup on error
                if (_diskProvider.FileExists(path))
                {
                    _diskProvider.DeleteFile(path);
                }
                throw;
            }
        }

        private string GetBackupPath(NzbDrone.Core.Backup.Backup backup)
        {
            return Path.Combine(_backupService.GetBackupFolder(backup.Type), backup.Name);
        }

        private int GetBackupId(NzbDrone.Core.Backup.Backup backup)
        {
            return HashConverter.GetHashInt31($"backup-{backup.Type}-{backup.Name}");
        }

        private NzbDrone.Core.Backup.Backup GetBackup(int id)
        {
            return _backupService.GetBackups().SingleOrDefault(b => GetBackupId(b) == id);
        }
    }

    public class MigrationDecisionRequest
    {
        public Dictionary<string, string> Decisions { get; set; }
    }

    public class UploadMigrationDecisionRequest
    {
        public string TempFileName { get; set; }
        public Dictionary<string, string> Decisions { get; set; }
    }
}
