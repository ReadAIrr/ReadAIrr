using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.BookImport.Manual;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Http.REST.Attributes;
using NzbDrone.SignalR;
using Readarr.Api.V1.ManualImport;
using Readarr.Http;
using Readarr.Http.REST;
using BadRequestException = NzbDrone.Core.Exceptions.BadRequestException;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace Readarr.Api.V1.BookFiles
{
    [V1ApiController]
    public class BookFileController : RestControllerWithSignalR<BookFileResource, BookFile>,
                                 IHandle<BookFileAddedEvent>,
                                 IHandle<BookFileDeletedEvent>
    {
        private readonly IMediaFileService _mediaFileService;
        private readonly IDeleteMediaFiles _mediaFileDeletionService;
        private readonly IMetadataTagService _metadataTagService;
        private readonly IManualImportService _manualImportService;
        private readonly IAuthorService _authorService;
        private readonly IBookService _bookService;
        private readonly IUpgradableSpecification _upgradableSpecification;
        private readonly IUnmappedIdentificationSuggestionService _unmappedIdentificationSuggestionService;

        public BookFileController(IBroadcastSignalRMessage signalRBroadcaster,
                               IMediaFileService mediaFileService,
                               IDeleteMediaFiles mediaFileDeletionService,
                               IMetadataTagService metadataTagService,
                               IManualImportService manualImportService,
                               IAuthorService authorService,
                               IBookService bookService,
                               IUpgradableSpecification upgradableSpecification,
                               IUnmappedIdentificationSuggestionService unmappedIdentificationSuggestionService)
            : base(signalRBroadcaster)
        {
            _mediaFileService = mediaFileService;
            _mediaFileDeletionService = mediaFileDeletionService;
            _metadataTagService = metadataTagService;
            _manualImportService = manualImportService;
            _authorService = authorService;
            _bookService = bookService;
            _upgradableSpecification = upgradableSpecification;
            _unmappedIdentificationSuggestionService = unmappedIdentificationSuggestionService;
        }

        private BookFileResource MapToResource(BookFile bookFile)
        {
            if (bookFile.EditionId > 0 && bookFile.Author != null && bookFile.Author.Value != null)
            {
                return bookFile.ToResource(bookFile.Author.Value, _upgradableSpecification);
            }
            else
            {
                return bookFile.ToResource();
            }
        }

        protected override BookFileResource GetResourceById(int id)
        {
            var resource = MapToResource(_mediaFileService.Get(id));
            resource.AudioTags = _metadataTagService.ReadTags((FileInfoBase)new FileInfo(resource.Path));
            return resource;
        }

        [HttpGet]
        public List<BookFileResource> GetBookFiles(int? authorId, [FromQuery]List<int> bookFileIds, [FromQuery(Name="bookId")]List<int> bookIds, bool? unmapped)
        {
            if (!authorId.HasValue && !bookFileIds.Any() && !bookIds.Any() && !unmapped.HasValue)
            {
                throw new BadRequestException("authorId, bookId, bookFileIds or unmapped must be provided");
            }

            if (unmapped.HasValue && unmapped.Value)
            {
                var files = _mediaFileService.GetUnmappedFiles();
                return MapUnmappedToResources(files);
            }

            if (authorId.HasValue && !bookIds.Any())
            {
                var author = _authorService.GetAuthor(authorId.Value);

                return _mediaFileService.GetFilesByAuthor(authorId.Value).ConvertAll(f => f.ToResource(author, _upgradableSpecification));
            }

            if (bookIds.Any())
            {
                var result = new List<BookFileResource>();
                foreach (var bookId in bookIds)
                {
                    var book = _bookService.GetBook(bookId);
                    var bookAuthor = _authorService.GetAuthor(book.AuthorId);
                    result.AddRange(_mediaFileService.GetFilesByBook(book.Id).ConvertAll(f => f.ToResource(bookAuthor, _upgradableSpecification)));
                }

                return result;
            }
            else
            {
                // trackfiles will come back with the author already populated
                var bookFiles = _mediaFileService.Get(bookFileIds);
                return bookFiles.ConvertAll(e => MapToResource(e));
            }
        }

        [RestPutById]
        public ActionResult<BookFileResource> SetQuality([FromBody] BookFileResource bookFileResource)
        {
            var bookFile = _mediaFileService.Get(bookFileResource.Id);
            bookFile.Quality = bookFileResource.Quality;
            _mediaFileService.Update(bookFile);
            return Accepted(bookFile.Id);
        }

        [HttpPut("editor")]
        public IActionResult SetQuality([FromBody] BookFileListResource resource)
        {
            var bookFiles = _mediaFileService.Get(resource.BookFileIds);

            foreach (var bookFile in bookFiles)
            {
                if (resource.Quality != null)
                {
                    bookFile.Quality = resource.Quality;
                }
            }

            _mediaFileService.Update(bookFiles);

            return Accepted(bookFiles.ConvertAll(f => f.ToResource(bookFiles.First().Author.Value, _upgradableSpecification)));
        }

        [HttpPost("unmapped/retry")]
        public ActionResult<List<BookFileResource>> RetryUnmappedIdentify([FromBody] BookFileListResource resource)
        {
            resource.BookFileIds = resource.BookFileIds ?? new List<int>();
            var bookFiles = _mediaFileService.Get(resource.BookFileIds).Where(x => x.EditionId == 0).ToList();

            return Accepted(MapUnmappedToResources(bookFiles));
        }

        [HttpPut("unmapped/reviewed")]
        public ActionResult<List<BookFileResource>> SetUnmappedReviewed([FromBody] BookFileListResource resource)
        {
            resource.BookFileIds = resource.BookFileIds ?? new List<int>();
            var reviewed = resource.Reviewed ?? true;
            var bookFiles = _mediaFileService.Get(resource.BookFileIds).Where(x => x.EditionId == 0).ToList();

            foreach (var bookFile in bookFiles)
            {
                bookFile.Reviewed = reviewed;
            }

            _mediaFileService.Update(bookFiles);

            return Accepted(MapUnmappedToResources(bookFiles));
        }

        [HttpPost("unmapped/ai-review")]
        public ActionResult<List<BookFileResource>> ReviewUnmappedWithAi([FromBody] BookFileListResource resource)
        {
            resource.BookFileIds = resource.BookFileIds ?? new List<int>();
            var bookFiles = _mediaFileService.Get(resource.BookFileIds).Where(x => x.EditionId == 0).ToList();
            var resources = MapUnmappedToResources(bookFiles);
            var suggestions = _unmappedIdentificationSuggestionService.ReviewWithAi(resources);

            AddSuggestions(resources, suggestions);

            return Accepted(resources);
        }

        [HttpPost("unmapped/deep-identify")]
        public ActionResult<List<BookFileResource>> DeepIdentifyUnmappedAudio([FromBody] BookFileListResource resource)
        {
            resource.BookFileIds = resource.BookFileIds ?? new List<int>();
            var bookFiles = _mediaFileService.Get(resource.BookFileIds).Where(x => x.EditionId == 0).ToList();
            var resources = MapUnmappedToResources(bookFiles);
            var suggestions = _unmappedIdentificationSuggestionService.DeepIdentifyAudio(resources);

            AddSuggestions(resources, suggestions);

            return Accepted(resources);
        }

        [RestDeleteById]
        public void DeleteBookFile(int id)
        {
            var bookFile = _mediaFileService.Get(id);

            if (bookFile == null)
            {
                throw new NzbDroneClientException(HttpStatusCode.NotFound, "Book file not found");
            }

            if (bookFile.EditionId > 0 && bookFile.Author != null && bookFile.Author.Value != null)
            {
                _mediaFileDeletionService.DeleteTrackFile(bookFile.Author.Value, bookFile);
            }
            else
            {
                _mediaFileDeletionService.DeleteTrackFile(bookFile, "Unmapped_Files");
            }
        }

        [HttpDelete("bulk")]
        public object DeleteTrackFiles([FromBody] BookFileListResource resource)
        {
            var bookFiles = _mediaFileService.Get(resource.BookFileIds);

            foreach (var bookFile in bookFiles)
            {
                if (bookFile.EditionId > 0 && bookFile.Author != null && bookFile.Author.Value != null)
                {
                    _mediaFileDeletionService.DeleteTrackFile(bookFile.Author.Value, bookFile);
                }
                else
                {
                    _mediaFileDeletionService.DeleteTrackFile(bookFile, "Unmapped_Files");
                }
            }

            return new { };
        }

        private List<BookFileResource> MapUnmappedToResources(List<BookFile> files)
        {
            var reviewItems = _manualImportService.GetMediaFiles(files.Select(x => x.Path).ToList(), null, false)
                                                  .GroupBy(x => x.Path, PathEqualityComparer.Instance)
                                                  .ToDictionary(x => x.Key, x => x.First(), PathEqualityComparer.Instance);

            return files.ConvertAll(file =>
            {
                var resource = MapToResource(file);

                if (reviewItems.TryGetValue(file.Path, out var reviewItem))
                {
                    resource.Review = reviewItem.ToReviewResource(file.Reviewed);
                    resource.AudioTags = reviewItem.Tags;
                }

                return resource;
            });
        }

        private static void AddSuggestions(List<BookFileResource> resources, List<ManualImportIdentificationSuggestionResource> suggestions)
        {
            foreach (var resource in resources)
            {
                resource.Review ??= new ManualImportReviewResource
                {
                    Status = "unknown",
                    StatusLabel = "Unknown",
                    Suggestions = new List<ManualImportIdentificationSuggestionResource>()
                };

                resource.Review.Suggestions ??= new List<ManualImportIdentificationSuggestionResource>();
                resource.Review.Suggestions.AddRange(suggestions.Where(x => string.Equals(x.Path, resource.Path, global::System.StringComparison.OrdinalIgnoreCase)));
            }
        }

        [NonAction]
        public void Handle(BookFileAddedEvent message)
        {
            BroadcastResourceChange(ModelAction.Updated, MapToResource(message.BookFile));
        }

        [NonAction]
        public void Handle(BookFileDeletedEvent message)
        {
            BroadcastResourceChange(ModelAction.Deleted, MapToResource(message.BookFile));
        }
    }
}
