using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Books;
using NzbDrone.Core.Books.Calibre;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.BookImport;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.RootFolders;

namespace NzbDrone.Core.MediaFiles
{
    public interface IDiskScanService
    {
        void Scan(List<string> folders = null, FilterFilesType filter = FilterFilesType.Known, bool addNewAuthors = false, List<int> authorIds = null);
        IFileInfo[] GetBookFiles(string path, bool allDirectories = true);
        IEnumerable<IFileInfo> EnumerateBookFiles(string path, bool allDirectories = true);
        string[] GetNonBookFiles(string path, bool allDirectories = true);
        List<IFileInfo> FilterFiles(string basePath, IEnumerable<IFileInfo> files);
        List<string> FilterPaths(string basePath, IEnumerable<string> paths);
    }

    public class DiskScanService :
        IDiskScanService,
        IExecute<RescanFoldersCommand>
    {
        public static readonly Regex ExcludedSubFoldersRegex = new Regex(@"(?:\\|\/|^)(?:extras|@eadir|extrafanart|plex versions|\.[^\\/]+)(?:\\|\/)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static readonly Regex ExcludedFilesRegex = new Regex(@"^\._|^Thumbs\.db$|^\.DS_store$|\.partial~$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        internal const int ImportDecisionBatchSize = 100;

        private readonly IConfigService _configService;
        private readonly IDiskProvider _diskProvider;
        private readonly ICalibreProxy _calibre;
        private readonly IMediaFileService _mediaFileService;
        private readonly IMakeImportDecision _importDecisionMaker;
        private readonly IImportApprovedBooks _importApprovedTracks;
        private readonly IAuthorService _authorService;
        private readonly IMediaFileTableCleanupService _mediaFileTableCleanupService;
        private readonly IRootFolderService _rootFolderService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public DiskScanService(IConfigService configService,
                               IDiskProvider diskProvider,
                               ICalibreProxy calibre,
                               IMediaFileService mediaFileService,
                               IMakeImportDecision importDecisionMaker,
                               IImportApprovedBooks importApprovedTracks,
                               IAuthorService authorService,
                               IRootFolderService rootFolderService,
                               IMediaFileTableCleanupService mediaFileTableCleanupService,
                               IEventAggregator eventAggregator,
                               Logger logger)
        {
            _configService = configService;
            _diskProvider = diskProvider;
            _calibre = calibre;

            _mediaFileService = mediaFileService;
            _importDecisionMaker = importDecisionMaker;
            _importApprovedTracks = importApprovedTracks;
            _authorService = authorService;
            _mediaFileTableCleanupService = mediaFileTableCleanupService;
            _rootFolderService = rootFolderService;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public void Scan(List<string> folders = null, FilterFilesType filter = FilterFilesType.Known, bool addNewAuthors = false, List<int> authorIds = null)
        {
            if (folders == null)
            {
                folders = _rootFolderService.All().Select(x => x.Path).ToList();
            }

            if (authorIds == null)
            {
                authorIds = new List<int>();
            }

            var musicFilesStopwatch = Stopwatch.StartNew();
            var config = new ImportDecisionMakerConfig
            {
                Filter = filter,
                IncludeExisting = true,
                AddNewAuthors = addNewAuthors
            };

            var totalFilesFound = 0;
            var totalDecisions = 0;
            var totalApproved = 0;
            var totalRejected = 0;
            var totalInserted = 0;
            var totalUpdated = 0;

            foreach (var folder in folders)
            {
                var folderStopwatch = Stopwatch.StartNew();

                // We could be scanning a root folder or a subset of a root folder.  If it's a subset,
                // check if the root folder exists before cleaning.
                var rootFolder = _rootFolderService.GetBestRootFolder(folder);

                if (rootFolder == null)
                {
                    _logger.Error("Not scanning {0}, it's not a subdirectory of a defined root folder", folder);
                    return;
                }

                var folderExists = _diskProvider.FolderExists(folder);

                if (!folderExists)
                {
                    if (!_diskProvider.FolderExists(rootFolder.Path))
                    {
                        _logger.Warn("Authors' root folder ({0}) doesn't exist.", rootFolder.Path);
                        var skippedAuthors = _authorService.GetAuthors(authorIds);
                        skippedAuthors.ForEach(x => _eventAggregator.PublishEvent(new AuthorScanSkippedEvent(x, AuthorScanSkippedReason.RootFolderDoesNotExist)));
                        return;
                    }

                    if (_diskProvider.FolderEmpty(rootFolder.Path))
                    {
                        _logger.Warn("Authors' root folder ({0}) is empty.", rootFolder.Path);
                        var skippedAuthors = _authorService.GetAuthors(authorIds);
                        skippedAuthors.ForEach(x => _eventAggregator.PublishEvent(new AuthorScanSkippedEvent(x, AuthorScanSkippedReason.RootFolderIsEmpty)));
                        return;
                    }
                }

                if (!folderExists)
                {
                    _logger.Debug("Specified scan folder ({0}) doesn't exist.", folder);

                    CleanMediaFiles(folder, new List<string>());
                    continue;
                }

                _logger.ProgressInfo("Scanning {0}", folder);

                var filePaths = new List<string>();
                var folderStats = ProcessFolderFileBatches(folder, EnumerateFilteredBookFiles(folder), filePaths, config);
                totalFilesFound += folderStats.FileCount;

                if (folderStats.FileCount == 0)
                {
                    _logger.Warn("Scan folder {0} is empty.", folder);
                    continue;
                }

                CleanMediaFiles(folder, filePaths);

                totalDecisions += folderStats.DecisionCount;
                totalApproved += folderStats.ApprovedCount;
                totalRejected += folderStats.RejectedCount;
                totalInserted += folderStats.InsertedCount;
                totalUpdated += folderStats.UpdatedCount;

                folderStopwatch.Stop();
                _logger.ProgressInfo("Completed scan of {0}: {1} files, {2} decisions, {3} accepted, {4} rejected, {5} inserted, {6} updated [{7}]",
                    folder,
                    folderStats.FileCount,
                    folderStats.DecisionCount,
                    folderStats.ApprovedCount,
                    folderStats.RejectedCount,
                    folderStats.InsertedCount,
                    folderStats.UpdatedCount,
                    folderStopwatch.Elapsed);
            }

            musicFilesStopwatch.Stop();
            _logger.Debug("Scan/import complete for {0} folders: {1} files, {2} decisions, {3} accepted, {4} rejected, {5} inserted, {6} updated [{7}]",
                folders.Count,
                totalFilesFound,
                totalDecisions,
                totalApproved,
                totalRejected,
                totalInserted,
                totalUpdated,
                musicFilesStopwatch.Elapsed);

            var authors = _authorService.GetAuthors(authorIds);
            foreach (var author in authors)
            {
                CompletedScanning(author);
            }

            _logger.Debug("Book scan complete for:\n{0} [{1}]", folders.ConcatToString("\n"), musicFilesStopwatch.Elapsed);
        }

        private ScanBatchStats ProcessFolderFileBatches(string folder, IEnumerable<IFileInfo> files, List<string> filePaths, ImportDecisionMakerConfig config)
        {
            var stats = new ScanBatchStats();
            var batchNumber = 0;
            var batchFiles = new List<IFileInfo>(ImportDecisionBatchSize);

            foreach (var file in files)
            {
                stats.FileCount++;
                filePaths.Add(file.FullName);
                batchFiles.Add(file);

                if (batchFiles.Count < ImportDecisionBatchSize)
                {
                    continue;
                }

                batchNumber++;
                ProcessScanBatch(folder, batchNumber, batchFiles, stats, config);
                batchFiles = new List<IFileInfo>(ImportDecisionBatchSize);
            }

            if (batchFiles.Any())
            {
                batchNumber++;
                ProcessScanBatch(folder, batchNumber, batchFiles, stats, config);
            }

            return stats;
        }

        private void ProcessScanBatch(string folder, int batchNumber, List<IFileInfo> batchFiles, ScanBatchStats stats, ImportDecisionMakerConfig config)
        {
            var batchStopwatch = Stopwatch.StartNew();

            _logger.ProgressInfo("Making import decisions for {0} batch {1} ({2} files)", folder, batchNumber, batchFiles.Count);

            var decisions = _importDecisionMaker.GetImportDecisions(batchFiles, null, null, config);
            var approved = decisions.Count(x => x.Approved);
            var rejected = decisions.Count - approved;

            _importApprovedTracks.Import(decisions, false);

            // Decisions may have been filtered to just new files. Anything new and approved will have been inserted.
            // Now make sure anything new but not approved gets inserted.
            var decisionPaths = decisions.Select(x => x.Item.Path).ToList();
            var knownFiles = decisionPaths.Any() ? _mediaFileService.GetFileWithPath(decisionPaths) : new List<BookFile>();

            var newFiles = decisions
                .ExceptBy(x => x.Item.Path, knownFiles, x => x.Path, PathEqualityComparer.Instance)
                .Select(decision => new BookFile
                {
                    Path = decision.Item.Path,
                    CalibreId = decision.Item.CalibreId,
                    Part = decision.Item.Part,
                    PartCount = decision.Item.PartCount,
                    Size = decision.Item.Size,
                    Modified = decision.Item.Modified,
                    DateAdded = DateTime.UtcNow,
                    Quality = decision.Item.Quality,
                    MediaInfo = decision.Item.FileTrackInfo.MediaInfo,
                    Edition = decision.Item.Edition
                })
                .ToList();

            if (newFiles.Any())
            {
                _mediaFileService.AddMany(newFiles);
            }

            var updatedFiles = knownFiles
                .Join(decisions,
                      x => x.Path,
                      x => x.Item.Path,
                      (file, decision) => new
                      {
                          File = file,
                          Item = decision.Item
                      },
                      PathEqualityComparer.Instance)
                .Where(x => x.File.Size != x.Item.Size ||
                       Math.Abs((x.File.Modified - x.Item.Modified).TotalSeconds) > 1)
                .Select(x =>
                {
                    x.File.Size = x.Item.Size;
                    x.File.Modified = x.Item.Modified;
                    x.File.MediaInfo = x.Item.FileTrackInfo.MediaInfo;
                    x.File.Quality = x.Item.Quality;
                    return x.File;
                })
                .ToList();

            if (updatedFiles.Any())
            {
                _mediaFileService.Update(updatedFiles);
            }

            batchStopwatch.Stop();

            stats.DecisionCount += decisions.Count;
            stats.ApprovedCount += approved;
            stats.RejectedCount += rejected;
            stats.InsertedCount += newFiles.Count;
            stats.UpdatedCount += updatedFiles.Count;

            _logger.Debug("Completed scan batch {0} for {1}: {2} decisions, {3} accepted, {4} rejected, {5} inserted, {6} updated [{7}]",
                batchNumber,
                folder,
                decisions.Count,
                approved,
                rejected,
                newFiles.Count,
                updatedFiles.Count,
                batchStopwatch.Elapsed);
        }

        private void CleanMediaFiles(string folder, List<string> mediaFileList)
        {
            _logger.Debug($"Cleaning up media files in DB [{folder}]");
            _mediaFileTableCleanupService.Clean(folder, mediaFileList);
        }

        private void CompletedScanning(Author author)
        {
            _logger.Info("Completed scanning disk for {0}", author.Name);
            _eventAggregator.PublishEvent(new AuthorScannedEvent(author));
        }

        private IEnumerable<IFileInfo> EnumerateFilteredBookFiles(string folder)
        {
            foreach (var file in EnumerateBookFiles(folder))
            {
                if (ExcludedSubFoldersRegex.IsMatch(folder.GetRelativePath(file.FullName)) ||
                    ExcludedFilesRegex.IsMatch(file.Name))
                {
                    continue;
                }

                yield return file;
            }
        }

        public IFileInfo[] GetBookFiles(string path, bool allDirectories = true)
        {
            var mediaFileList = EnumerateBookFiles(path, allDirectories).ToArray();

            _logger.Debug("{0} book files were found in {1}", mediaFileList.Length, path);

            return mediaFileList;
        }

        public IEnumerable<IFileInfo> EnumerateBookFiles(string path, bool allDirectories = true)
        {
            IEnumerable<IFileInfo> filesOnDisk;

            var rootFolder = _rootFolderService.GetBestRootFolder(path);

            _logger.Trace(rootFolder.ToJson());

            if (rootFolder != null && rootFolder.IsCalibreLibrary && rootFolder.CalibreSettings != null)
            {
                _logger.Info($"Getting book list from calibre for {path}");
                var paths = _calibre.GetAllBookFilePaths(rootFolder.CalibreSettings);
                var folderPaths = paths.Where(x => path.IsParentPath(x));

                filesOnDisk = folderPaths.Select(x => _diskProvider.GetFileInfo(x));
            }
            else
            {
                _logger.Debug("Scanning '{0}' for ebook files", path);

                filesOnDisk = _diskProvider.EnumerateFileInfos(path, allDirectories);
            }

            foreach (var file in filesOnDisk)
            {
                if (MediaFileExtensions.AllExtensions.Contains(file.Extension))
                {
                    yield return file;
                }
            }
        }

        public string[] GetNonBookFiles(string path, bool allDirectories = true)
        {
            _logger.Debug("Scanning '{0}' for non-ebook files", path);

            var filesOnDisk = _diskProvider.GetFiles(path, allDirectories).ToList();

            var mediaFileList = filesOnDisk.Where(file => !MediaFileExtensions.AllExtensions.Contains(Path.GetExtension(file)))
                                           .ToList();

            _logger.Trace("{0} files were found in {1}", filesOnDisk.Count, path);
            _logger.Debug("{0} non-ebook files were found in {1}", mediaFileList.Count, path);

            return mediaFileList.ToArray();
        }

        public List<string> FilterPaths(string basePath, IEnumerable<string> paths)
        {
            return paths.Where(file => !ExcludedSubFoldersRegex.IsMatch(basePath.GetRelativePath(file)))
                        .Where(file => !ExcludedFilesRegex.IsMatch(Path.GetFileName(file)))
                        .ToList();
        }

        public List<IFileInfo> FilterFiles(string basePath, IEnumerable<IFileInfo> files)
        {
            return files.Where(file => !ExcludedSubFoldersRegex.IsMatch(basePath.GetRelativePath(file.FullName)))
                        .Where(file => !ExcludedFilesRegex.IsMatch(file.Name))
                        .ToList();
        }

        public void Execute(RescanFoldersCommand message)
        {
            Scan(message.Folders, message.Filter, message.AddNewAuthors, message.AuthorIds);
        }

        private sealed class ScanBatchStats
        {
            public int FileCount { get; set; }
            public int DecisionCount { get; set; }
            public int ApprovedCount { get; set; }
            public int RejectedCount { get; set; }
            public int InsertedCount { get; set; }
            public int UpdatedCount { get; set; }
        }
    }
}
