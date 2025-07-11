using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Books.Calibre;
using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Books.Events;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Extras;
using NzbDrone.Core.History;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.RootFolders;

namespace NzbDrone.Core.MediaFiles.BookImport
{
    public interface IImportApprovedBooks
    {
        List<ImportResult> Import(List<ImportDecision<LocalBook>> decisions, bool replaceExisting, DownloadClientItem downloadClientItem = null, ImportMode importMode = ImportMode.Auto);
    }

    public class ImportApprovedBooks : IImportApprovedBooks
    {
        private static readonly RegexReplace PadNumbers = new RegexReplace(@"\d+", n => n.Value.PadLeft(9, '0'), RegexOptions.Compiled);

        private readonly IUpgradeMediaFiles _bookFileUpgrader;
        private readonly IMediaFileService _mediaFileService;
        private readonly IMetadataTagService _metadataTagService;
        private readonly IAuthorService _authorService;
        private readonly IAddAuthorService _addAuthorService;
        private readonly IBookService _bookService;
        private readonly IEditionService _editionService;
        private readonly IRootFolderService _rootFolderService;
        private readonly IRecycleBinProvider _recycleBinProvider;
        private readonly IExtraService _extraService;
        private readonly IDiskProvider _diskProvider;
        private readonly IHistoryService _historyService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public ImportApprovedBooks(IUpgradeMediaFiles bookFileUpgrader,
                                   IMediaFileService mediaFileService,
                                   IMetadataTagService metadataTagService,
                                   IAuthorService authorService,
                                   IAddAuthorService addAuthorService,
                                   IBookService bookService,
                                   IEditionService editionService,
                                   IRootFolderService rootFolderService,
                                   IRecycleBinProvider recycleBinProvider,
                                   IExtraService extraService,
                                   IDiskProvider diskProvider,
                                   IHistoryService historyService,
                                   IEventAggregator eventAggregator,
                                   IManageCommandQueue commandQueueManager,
                                   Logger logger)
        {
            _bookFileUpgrader = bookFileUpgrader;
            _mediaFileService = mediaFileService;
            _metadataTagService = metadataTagService;
            _authorService = authorService;
            _addAuthorService = addAuthorService;
            _bookService = bookService;
            _editionService = editionService;
            _rootFolderService = rootFolderService;
            _recycleBinProvider = recycleBinProvider;
            _extraService = extraService;
            _diskProvider = diskProvider;
            _historyService = historyService;
            _eventAggregator = eventAggregator;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
        }

        public List<ImportResult> Import(List<ImportDecision<LocalBook>> decisions, bool replaceExisting, DownloadClientItem downloadClientItem = null, ImportMode importMode = ImportMode.Auto)
        {
            var importResults = new List<ImportResult>();
            var allImportedTrackFiles = new List<BookFile>();
            var allOldTrackFiles = new List<BookFile>();
            var addedAuthors = new List<Author>();
            var addedBooks = new List<Book>();

            var bookDecisions = decisions.Where(e => e.Item.Book != null && e.Approved)
                .GroupBy(e => e.Item.Book.ForeignBookId).ToList();

            var iDecision = 1;
            foreach (var bookDecision in bookDecisions)
            {
                _logger.ProgressInfo("Importing book {0}/{1} {2}", iDecision++, bookDecisions.Count, bookDecision.First().Item.Book);

                var decisionList = bookDecision.ToList();

                var author = EnsureAuthorAdded(decisionList, addedAuthors);

                if (author == null)
                {
                    // failed to add the author, carry on with next book
                    continue;
                }

                var book = EnsureBookAdded(decisionList, addedBooks);

                if (book == null)
                {
                    // failed to add the book, carry on with next one
                    continue;
                }

                var edition = EnsureEditionAdded(decisionList);

                if (edition == null)
                {
                    // failed to add the edition, carry on with next one
                    continue;
                }

                // if (replaceExisting)
                // {
                //     RemoveExistingTrackFiles(author, book);
                // }

                // Make sure part numbers are populated for audiobooks
                // If all audio files and all part numbers are zero, set them by filename order
                if (decisionList.All(b => MediaFileExtensions.AudioExtensions.Contains(Path.GetExtension(b.Item.Path)) && b.Item.Part == 0))
                {
                    var part = 1;
                    foreach (var d in decisionList.OrderBy(x => PadNumbers.Replace(x.Item.Path)))
                    {
                        d.Item.Part = part++;
                    }
                }

                // set the correct release to be monitored before importing the new files
                var newRelease = bookDecision.First().Item.Edition;
                _logger.Debug("Updating release to {0}", newRelease);
                book.Editions = _editionService.SetMonitored(newRelease);

                // Publish book edited event.
                // Deliberately don't put in the old book since we don't want to trigger an AuthorScan.
                _eventAggregator.PublishEvent(new BookEditedEvent(book, book));
            }

            var qualifiedImports = decisions.Where(c => c.Approved)
                .GroupBy(c => c.Item.Author.Id, (i, s) => s
                         .OrderByDescending(c => c.Item.Quality, new QualityModelComparer(s.First().Item.Author.QualityProfile))
                         .ThenByDescending(c => c.Item.Size))
                .SelectMany(c => c)
                .ToList();

            _logger.ProgressInfo("Importing {0} files", qualifiedImports.Count);
            _logger.Debug("Importing {0} files. Replace existing: {1}", qualifiedImports.Count, replaceExisting);

            var filesToAdd = new List<BookFile>(qualifiedImports.Count);
            var trackImportedEvents = new List<TrackImportedEvent>(qualifiedImports.Count);

            foreach (var importDecision in qualifiedImports)
            {
                var localTrack = importDecision.Item;
                var oldFiles = new List<BookFile>();

                try
                {
                    //check if already imported
                    if (importResults.Where(r => r.ImportDecision.Item.Book.Id == localTrack.Book.Id).Any(r => r.ImportDecision.Item.Part == localTrack.Part))
                    {
                        importResults.Add(new ImportResult(importDecision, "Book has already been imported"));
                        continue;
                    }

                    localTrack.Book.Author = localTrack.Author;

                    var bookFile = new BookFile
                    {
                        Path = localTrack.Path.CleanFilePath(),
                        CalibreId = localTrack.CalibreId,
                        Part = localTrack.Part,
                        PartCount = localTrack.PartCount,
                        Size = localTrack.Size,
                        Modified = localTrack.Modified,
                        DateAdded = DateTime.UtcNow,
                        ReleaseGroup = localTrack.ReleaseGroup,
                        Quality = localTrack.Quality,
                        MediaInfo = localTrack.FileTrackInfo.MediaInfo,
                        EditionId = localTrack.Edition.Id,
                        Author = localTrack.Author,
                        Edition = localTrack.Edition
                    };

                    if (downloadClientItem?.DownloadId.IsNotNullOrWhiteSpace() == true)
                    {
                        var grabHistory = _historyService.FindByDownloadId(downloadClientItem.DownloadId)
                            .OrderByDescending(h => h.Date)
                            .FirstOrDefault(h => h.EventType == EntityHistoryEventType.Grabbed);

                        if (Enum.TryParse(grabHistory?.Data.GetValueOrDefault("indexerFlags"), true, out IndexerFlags flags))
                        {
                            bookFile.IndexerFlags = flags;
                        }
                    }
                    else
                    {
                        bookFile.IndexerFlags = localTrack.IndexerFlags;
                    }

                    bool copyOnly;
                    switch (importMode)
                    {
                        default:
                        case ImportMode.Auto:
                            copyOnly = downloadClientItem != null && !downloadClientItem.CanMoveFiles;
                            break;
                        case ImportMode.Move:
                            copyOnly = false;
                            break;
                        case ImportMode.Copy:
                            copyOnly = true;
                            break;
                    }

                    if (!localTrack.ExistingFile)
                    {
                        bookFile.SceneName = GetSceneReleaseName(downloadClientItem);

                        var moveResult = _bookFileUpgrader.UpgradeBookFile(bookFile, localTrack, copyOnly);
                        oldFiles = moveResult.OldFiles;
                    }
                    else
                    {
                        // Delete existing files from the DB mapped to this path
                        var previousFile = _mediaFileService.GetFileWithPath(bookFile.Path);

                        if (previousFile != null)
                        {
                            _mediaFileService.Delete(previousFile, DeleteMediaFileReason.ManualOverride);

                            if (bookFile.CalibreId == 0 && previousFile.CalibreId != 0)
                            {
                                bookFile.CalibreId = previousFile.CalibreId;
                            }
                        }

                        _metadataTagService.WriteTags(bookFile, false);
                    }

                    filesToAdd.Add(bookFile);
                    importResults.Add(new ImportResult(importDecision));

                    if (!localTrack.ExistingFile)
                    {
                        _extraService.ImportTrack(localTrack, bookFile, copyOnly);
                    }

                    allImportedTrackFiles.Add(bookFile);
                    allOldTrackFiles.AddRange(oldFiles);

                    // create all the import events here, but we can't publish until the trackfiles have been
                    // inserted and ids created
                    trackImportedEvents.Add(new TrackImportedEvent(localTrack, bookFile, oldFiles, !localTrack.ExistingFile, downloadClientItem));
                }
                catch (RootFolderNotFoundException e)
                {
                    _logger.Warn(e, "Couldn't import book " + localTrack);
                    _eventAggregator.PublishEvent(new TrackImportFailedEvent(e, localTrack, !localTrack.ExistingFile, downloadClientItem));

                    importResults.Add(new ImportResult(importDecision, "Failed to import book, root folder missing."));
                }
                catch (DestinationAlreadyExistsException e)
                {
                    _logger.Warn(e, "Couldn't import book " + localTrack);
                    importResults.Add(new ImportResult(importDecision, "Failed to import book, destination already exists."));
                }
                catch (UnauthorizedAccessException e)
                {
                    _logger.Warn(e, "Couldn't import book " + localTrack);
                    _eventAggregator.PublishEvent(new TrackImportFailedEvent(e, localTrack, !localTrack.ExistingFile, downloadClientItem));

                    importResults.Add(new ImportResult(importDecision, "Failed to import book, permissions error"));
                }
                catch (RecycleBinException e)
                {
                    _logger.Warn(e, "Couldn't import book " + localTrack);
                    _eventAggregator.PublishEvent(new TrackImportFailedEvent(e, localTrack, !localTrack.ExistingFile, downloadClientItem));

                    importResults.Add(new ImportResult(importDecision, "Failed to import book, unable to move existing file to the Recycle Bin."));
                }
                catch (CalibreException e)
                {
                    _logger.Warn(e, "Couldn't import book " + localTrack);

                    importResults.Add(new ImportResult(importDecision, "Failed to import book, error communicating with Calibre.  Check log for details."));
                }
                catch (Exception e)
                {
                    _logger.Warn(e, "Couldn't import book " + localTrack);
                    importResults.Add(new ImportResult(importDecision, "Failed to import book."));
                }
            }

            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            _mediaFileService.AddMany(filesToAdd);
            _logger.Debug("Inserted new trackfiles in {0}ms", watch.ElapsedMilliseconds);

            // now that trackfiles have been inserted and ids generated, publish the import events
            foreach (var trackImportedEvent in trackImportedEvents)
            {
                _eventAggregator.PublishEvent(trackImportedEvent);
            }

            var bookImports = importResults.Where(e => e.ImportDecision.Item.Book != null)
                .GroupBy(e => e.ImportDecision.Item.Book.Id).ToList();

            foreach (var bookImport in bookImports)
            {
                var book = bookImport.First().ImportDecision.Item.Book;
                var edition = book.Editions.Value.Single(x => x.Monitored);
                var author = bookImport.First().ImportDecision.Item.Author;

                if (bookImport.Where(e => e.Errors.Count == 0).ToList().Count > 0 && author != null && book != null)
                {
                    _eventAggregator.PublishEvent(new BookImportedEvent(
                        author,
                        book,
                        allImportedTrackFiles.Where(s => s.EditionId == edition.Id).ToList(),
                        allOldTrackFiles.Where(s => s.EditionId == edition.Id).ToList(),
                        replaceExisting,
                        downloadClientItem));
                }
            }

            //Adding all the rejected decisions
            importResults.AddRange(decisions.Where(c => !c.Approved)
                                            .Select(d => new ImportResult(d, d.Rejections.Select(r => r.Reason).ToArray())));

            // Refresh any authors we added
            if (addedAuthors.Any())
            {
                _commandQueueManager.Push(new BulkRefreshAuthorCommand(addedAuthors.Select(x => x.Id).ToList(), true));
            }

            var addedAuthorMetadataIds = addedAuthors.Select(x => x.AuthorMetadataId).ToHashSet();
            var booksToRefresh = addedBooks.Where(x => !addedAuthorMetadataIds.Contains(x.AuthorMetadataId)).ToList();

            if (booksToRefresh.Any())
            {
                _logger.Debug("Refreshing info for {0} new books", booksToRefresh.Count);
                _commandQueueManager.Push(new BulkRefreshBookCommand(booksToRefresh.Select(x => x.Id).ToList()));
            }

            return importResults;
        }

        private Author EnsureAuthorAdded(List<ImportDecision<LocalBook>> decisions, List<Author> addedAuthors)
        {
            var author = decisions.First().Item.Author;

            if (author.Id == 0)
            {
                var dbAuthor = _authorService.FindById(author.ForeignAuthorId);

                if (dbAuthor == null)
                {
                    _logger.Debug("Adding remote author {0}", author);

                    var path = decisions.First().Item.Path;
                    var rootFolder = _rootFolderService.GetBestRootFolder(path);

                    author.RootFolderPath = rootFolder.Path;
                    author.MetadataProfileId = rootFolder.DefaultMetadataProfileId;
                    author.QualityProfileId = rootFolder.DefaultQualityProfileId;
                    author.Monitored = rootFolder.DefaultMonitorOption != MonitorTypes.None;
                    author.MonitorNewItems = rootFolder.DefaultNewItemMonitorOption;
                    author.Tags = rootFolder.DefaultTags;
                    author.AddOptions = new AddAuthorOptions
                    {
                        SearchForMissingBooks = false,
                        Monitored = author.Monitored,
                        Monitor = rootFolder.DefaultMonitorOption
                    };

                    if (rootFolder.IsCalibreLibrary)
                    {
                        // calibre has author / book / files
                        author.Path = path.GetParentPath().GetParentPath();
                    }

                    try
                    {
                        dbAuthor = _addAuthorService.AddAuthor(author, false);

                        // this looks redundant but is necessary to get the LazyLoads populated
                        dbAuthor = _authorService.GetAuthor(dbAuthor.Id);
                        addedAuthors.Add(dbAuthor);
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Failed to add author {0}", author);
                        foreach (var decision in decisions)
                        {
                            decision.Reject(new Rejection("Failed to add missing author", RejectionType.Temporary));
                        }

                        return null;
                    }
                }

                // Put in the newly loaded author
                foreach (var decision in decisions)
                {
                    decision.Item.Author = dbAuthor;
                    decision.Item.Book.Author = dbAuthor;
                    decision.Item.Book.AuthorMetadataId = dbAuthor.AuthorMetadataId;
                }

                author = dbAuthor;
            }

            return author;
        }

        private Book EnsureBookAdded(List<ImportDecision<LocalBook>> decisions, List<Book> addedBooks)
        {
            var book = decisions.First().Item.Book;

            if (book.Id == 0)
            {
                var dbBook = _bookService.FindById(book.ForeignBookId);

                if (dbBook == null)
                {
                    _logger.Debug("Adding remote book {0}", book);

                    if (book.AuthorMetadataId == 0)
                    {
                        throw new InvalidOperationException("Cannot insert book with AuthorMetadataId = 0");
                    }

                    try
                    {
                        book.Monitored = book.Author.Value.Monitored;
                        book.Added = DateTime.UtcNow;
                        _bookService.InsertMany(new List<Book> { book });
                        addedBooks.Add(book);

                        book.Editions.Value.ForEach(x => x.BookId = book.Id);
                        _editionService.InsertMany(book.Editions.Value);

                        dbBook = _bookService.FindById(book.ForeignBookId);
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Failed to add book {0}", book);
                        RejectBook(decisions);

                        return null;
                    }
                }

                var edition = dbBook.Editions.Value.ExclusiveOrDefault(x => x.ForeignEditionId == decisions.First().Item.Edition.ForeignEditionId);
                if (edition == null)
                {
                    RejectBook(decisions);
                    return null;
                }

                // Populate the new DB book
                foreach (var decision in decisions)
                {
                    decision.Item.Book = dbBook;
                    decision.Item.Edition = edition;
                }

                book = dbBook;
            }

            return book;
        }

        private Edition EnsureEditionAdded(List<ImportDecision<LocalBook>> decisions)
        {
            var book = decisions.First().Item.Book;
            var edition = decisions.First().Item.Edition;

            if (edition.Id == 0)
            {
                var dbEdition = _editionService.GetEditionByForeignEditionId(edition.ForeignEditionId);

                if (dbEdition == null)
                {
                    _logger.Debug("Adding remote edition {0}", edition);

                    try
                    {
                        edition.BookId = book.Id;
                        edition.Monitored = false;
                        _editionService.InsertMany(new List<Edition> { edition });

                        dbEdition = _editionService.GetEditionByForeignEditionId(edition.ForeignEditionId);
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Failed to add edition {0}", edition);
                        RejectBook(decisions);

                        return null;
                    }

                    // Populate the new DB book
                    foreach (var decision in decisions)
                    {
                        decision.Item.Edition = dbEdition;
                    }

                    edition = dbEdition;
                }
            }

            return edition;
        }

        private void RejectBook(List<ImportDecision<LocalBook>> decisions)
        {
            foreach (var decision in decisions)
            {
                decision.Reject(new Rejection("Failed to add missing book", RejectionType.Temporary));
            }
        }

        private void RemoveExistingTrackFiles(Author author, Book book)
        {
            var rootFolder = _diskProvider.GetParentFolder(author.Path);
            var previousFiles = _mediaFileService.GetFilesByBook(book.Id);

            _logger.Debug("Deleting {0} existing files for {1}", previousFiles.Count, book);

            foreach (var previousFile in previousFiles)
            {
                var subfolder = rootFolder.GetRelativePath(_diskProvider.GetParentFolder(previousFile.Path));
                if (_diskProvider.FileExists(previousFile.Path))
                {
                    _logger.Debug("Removing existing book file: {0}", previousFile);
                    _recycleBinProvider.DeleteFile(previousFile.Path, subfolder);
                }

                _mediaFileService.Delete(previousFile, DeleteMediaFileReason.Upgrade);
            }
        }

        private string GetSceneReleaseName(DownloadClientItem downloadClientItem)
        {
            if (downloadClientItem != null)
            {
                var title = Parser.Parser.RemoveFileExtension(downloadClientItem.Title);

                var parsedTitle = Parser.Parser.ParseBookTitle(title);

                if (parsedTitle != null)
                {
                    return title;
                }
            }

            return null;
        }
    }
}
