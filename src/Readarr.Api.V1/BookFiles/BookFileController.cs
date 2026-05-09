using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
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
        private const int MaxUnmappedPageSize = 500;

        private readonly IMediaFileService _mediaFileService;
        private readonly IDeleteMediaFiles _mediaFileDeletionService;
        private readonly IMetadataTagService _metadataTagService;
        private readonly IManualImportService _manualImportService;
        private readonly IManualImportReviewSessionCache _reviewSessionCache;
        private readonly IAuthorService _authorService;
        private readonly IBookService _bookService;
        private readonly IUpgradableSpecification _upgradableSpecification;
        private readonly IUnmappedIdentificationSuggestionService _unmappedIdentificationSuggestionService;
        private readonly IAudioIntroTranscriptionService _audioIntroTranscriptionService;
        private readonly IContributorEvidenceRepository _contributorEvidenceRepository;
        private readonly IAudioTagEditService _audioTagEditService;
        private readonly IConfigService _configService;

        public BookFileController(IBroadcastSignalRMessage signalRBroadcaster,
                               IMediaFileService mediaFileService,
                               IDeleteMediaFiles mediaFileDeletionService,
                               IMetadataTagService metadataTagService,
                               IManualImportService manualImportService,
                               IManualImportReviewSessionCache reviewSessionCache,
                               IAuthorService authorService,
                               IBookService bookService,
                               IUpgradableSpecification upgradableSpecification,
                               IUnmappedIdentificationSuggestionService unmappedIdentificationSuggestionService,
                               IAudioIntroTranscriptionService audioIntroTranscriptionService,
                               IContributorEvidenceRepository contributorEvidenceRepository,
                               IAudioTagEditService audioTagEditService,
                               IConfigService configService)
            : base(signalRBroadcaster)
        {
            _mediaFileService = mediaFileService;
            _mediaFileDeletionService = mediaFileDeletionService;
            _metadataTagService = metadataTagService;
            _manualImportService = manualImportService;
            _reviewSessionCache = reviewSessionCache;
            _authorService = authorService;
            _bookService = bookService;
            _upgradableSpecification = upgradableSpecification;
            _unmappedIdentificationSuggestionService = unmappedIdentificationSuggestionService;
            _audioIntroTranscriptionService = audioIntroTranscriptionService;
            _contributorEvidenceRepository = contributorEvidenceRepository;
            _audioTagEditService = audioTagEditService;
            _configService = configService;
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

        [HttpGet("unmapped/paged")]
        public PagingResource<BookFileResource> GetUnmappedFilesPaged([FromQuery] PagingRequestResource paging, bool refresh = false)
        {
            var requestedPage = paging?.Page ?? 1;
            var requestedPageSize = paging?.PageSize ?? MaxUnmappedPageSize;
            var page = global::System.Math.Max(1, requestedPage);
            var pageSize = global::System.Math.Min(MaxUnmappedPageSize, global::System.Math.Max(1, requestedPageSize));

            var pagingSpec = new PagingSpec<BookFile>
            {
                Page = page,
                PageSize = pageSize,
                SortKey = nameof(BookFile.Path),
                SortDirection = SortDirection.Ascending
            };

            var result = _mediaFileService.GetUnmappedFiles(pagingSpec);

            return new PagingResource<BookFileResource>
            {
                Page = result.Page,
                PageSize = result.PageSize,
                SortKey = null,
                SortDirection = SortDirection.Default,
                TotalRecords = result.TotalRecords,
                Records = MapUnmappedToResources(result.Records, refresh, true)
            };
        }

        [RestPutById]
        public ActionResult<BookFileResource> SetQuality([FromBody] BookFileResource bookFileResource)
        {
            var bookFile = _mediaFileService.Get(bookFileResource.Id);
            bookFile.Quality = bookFileResource.Quality;
            _mediaFileService.Update(bookFile);
            return Accepted(bookFile.Id);
        }

        [HttpGet("{id:int}/audioTag")]
        public ActionResult<AudioTagEditPreview> GetAudioTagPreview(int id)
        {
            return _audioTagEditService.GetPreview(id);
        }

        [HttpPost("{id:int}/audioTag/preview")]
        public ActionResult<AudioTagEditPreview> PreviewAudioTagWrite(int id, [FromBody] AudioTagValues resource)
        {
            return _audioTagEditService.Preview(id, resource);
        }

        [HttpPut("{id:int}/audioTag")]
        public ActionResult<AudioTagEditPreview> WriteAudioTags(int id, [FromBody] AudioTagValues resource)
        {
            return Accepted(_audioTagEditService.Write(id, resource));
        }

        [HttpGet("audioTag/templates")]
        public ActionResult<List<AudioTagTemplateOption>> GetAudioTagTemplates()
        {
            return _audioTagEditService.GetTemplates();
        }

        [HttpPost("audioTag/templates/preview")]
        public ActionResult<AudioTagTemplatePreview> PreviewAudioTagTemplate([FromBody] AudioTagTemplateRequest resource)
        {
            return _audioTagEditService.PreviewTemplate(resource);
        }

        [HttpPut("audioTag/templates")]
        public ActionResult<AudioTagTemplatePreview> WriteAudioTagTemplate([FromBody] AudioTagTemplateRequest resource)
        {
            return Accepted(_audioTagEditService.WriteTemplate(resource));
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

            AddSuggestions(resources, suggestions, _configService.MinimumBookMatchSimilarity);
            AddContributorEvidence(resources, _contributorEvidenceRepository.GetByBookFileIds(resources.Select(x => x.Id)));

            return Accepted(resources);
        }

        [HttpPost("unmapped/deep-identify")]
        public ActionResult<List<BookFileResource>> DeepIdentifyUnmappedAudio([FromBody] BookFileListResource resource)
        {
            resource.BookFileIds = resource.BookFileIds ?? new List<int>();
            var bookFiles = _mediaFileService.Get(resource.BookFileIds).Where(x => x.EditionId == 0).ToList();
            var resources = MapUnmappedToResources(bookFiles);
            var suggestions = _unmappedIdentificationSuggestionService.DeepIdentifyAudio(resources);

            AddSuggestions(resources, suggestions, _configService.MinimumBookMatchSimilarity);
            AddContributorEvidence(resources, _contributorEvidenceRepository.GetByBookFileIds(resources.Select(x => x.Id)));

            return Accepted(resources);
        }

        [HttpPost("unmapped/deep-identify/queue")]
        public ActionResult<List<BookFileResource>> QueueDeepIdentifyUnmappedAudio([FromBody] BookFileListResource resource)
        {
            resource.BookFileIds = resource.BookFileIds ?? new List<int>();
            var bookFiles = _mediaFileService.Get(resource.BookFileIds).Where(x => x.EditionId == 0).ToList();
            var resources = MapUnmappedToResources(bookFiles);

            _unmappedIdentificationSuggestionService.QueueDeepIdentifyAudio(resources);

            return Accepted(MapUnmappedToResources(bookFiles));
        }

        [HttpPost("unmapped/suggestions/clear")]
        public ActionResult<List<BookFileResource>> ClearUnmappedSuggestions([FromBody] BookFileListResource resource)
        {
            resource.BookFileIds = resource.BookFileIds ?? new List<int>();
            var bookFiles = _mediaFileService.Get(resource.BookFileIds).Where(x => x.EditionId == 0).ToList();

            _unmappedIdentificationSuggestionService.Clear(bookFiles.Select(x => x.Id).ToList());

            return Accepted(MapUnmappedToResources(bookFiles));
        }

        [HttpPut("unmapped/contributor-evidence")]
        public ActionResult<List<BookFileResource>> SetUnmappedContributorEvidence([FromBody] ContributorEvidenceUpdateResource resource)
        {
            if (resource == null || resource.BookFileId <= 0)
            {
                throw new BadRequestException("bookFileId must be provided");
            }

            if (string.IsNullOrWhiteSpace(resource.Role))
            {
                throw new BadRequestException("role must be provided");
            }

            if (string.IsNullOrWhiteSpace(resource.DisplayName))
            {
                throw new BadRequestException("displayName must be provided");
            }

            var bookFile = _mediaFileService.Get(resource.BookFileId);

            if (bookFile == null || bookFile.EditionId > 0)
            {
                throw new NzbDroneClientException(HttpStatusCode.NotFound, "Unmapped book file not found");
            }

            var now = global::System.DateTime.UtcNow;
            var role = resource.Role.Trim();
            var displayName = resource.DisplayName.Trim();

            _contributorEvidenceRepository.DeleteByBookFileIdSourceAndRole(bookFile.Id, "manual", role);
            _contributorEvidenceRepository.Insert(new ContributorEvidence
            {
                BookFileId = bookFile.Id,
                Role = role,
                DisplayName = displayName,
                NormalizedName = ContributorEvidence.NormalizeName(displayName),
                Source = "manual",
                Confidence = resource.Confidence,
                RawValue = string.IsNullOrWhiteSpace(resource.RawValue) ? displayName : resource.RawValue,
                Created = now,
                Updated = now
            });

            return Accepted(MapUnmappedToResources(new List<BookFile> { bookFile }));
        }

        [HttpGet("unmapped/{id:int}/intro-preview")]
        public IActionResult GetUnmappedIntroPreview(int id)
        {
            var bookFile = _mediaFileService.Get(id);

            if (bookFile == null || bookFile.EditionId > 0)
            {
                throw new NzbDroneClientException(HttpStatusCode.NotFound, "Unmapped book file not found");
            }

            var resource = MapToResource(bookFile);
            var segment = _audioIntroTranscriptionService.ExtractPreview(resource);

            if (!segment.IsSuccess)
            {
                throw new NzbDroneClientException(HttpStatusCode.BadRequest, segment.Explanation);
            }

            return File(segment.Content, segment.ContentType ?? "audio/mpeg", segment.FileName ?? "readairr-intro.mp3");
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

        private List<BookFileResource> MapUnmappedToResources(List<BookFile> files, bool refreshReviewCache = false, bool useReviewCache = false)
        {
            var paths = files.Select(x => x.Path).ToList();
            var reviewItemsList = useReviewCache ?
                _reviewSessionCache.GetUnmappedReviewItems(files, false, refreshReviewCache, () => _manualImportService.GetMediaFiles(paths, null, false)).Value :
                _manualImportService.GetMediaFiles(paths, null, false);

            var reviewItems = reviewItemsList.GroupBy(x => x.Path, PathEqualityComparer.Instance)
                                             .ToDictionary(x => x.Key, x => x.First(), PathEqualityComparer.Instance);

            var resources = files.ConvertAll(file =>
            {
                var resource = MapToResource(file);

                if (reviewItems.TryGetValue(file.Path, out var reviewItem))
                {
                    resource.Review = reviewItem.ToReviewResource(file.Reviewed);
                    resource.AudioTags = reviewItem.Tags;
                }

                return resource;
            });

            AddSuggestions(resources, _unmappedIdentificationSuggestionService.GetPersisted(resources), _configService.MinimumBookMatchSimilarity);
            AddContributorEvidence(resources, _contributorEvidenceRepository.GetByBookFileIds(resources.Select(x => x.Id)));

            return resources;
        }

        private static void AddSuggestions(List<BookFileResource> resources, List<ManualImportIdentificationSuggestionResource> suggestions, int minimumMatchSimilarity)
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
                ManualImportReviewResourceMapper.ApplySuggestionEvidence(resource.Review);
                ManualImportReviewResourceMapper.ApplyMatchingCriteriaContext(resource.Review, minimumMatchSimilarity);
            }
        }

        private static void AddContributorEvidence(List<BookFileResource> resources, List<ContributorEvidence> evidence)
        {
            var evidenceByBookFileId = evidence
                .Where(x => x.BookFileId.HasValue)
                .GroupBy(x => x.BookFileId.Value)
                .ToDictionary(x => x.Key, x => x.Select(item => item.ToResource()).ToList());

            foreach (var resource in resources)
            {
                if (!evidenceByBookFileId.TryGetValue(resource.Id, out var contributorEvidence))
                {
                    contributorEvidence = new List<ContributorEvidenceResource>();
                }

                resource.ContributorEvidence = contributorEvidence;

                if (resource.Review != null)
                {
                    resource.Review.ContributorEvidence = contributorEvidence;
                }
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
