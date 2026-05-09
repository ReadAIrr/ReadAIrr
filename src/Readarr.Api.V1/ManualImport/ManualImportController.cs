using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NzbDrone.Common;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.BookImport.Manual;
using NzbDrone.Core.Qualities;
using Readarr.Api.V1.BookFiles;
using Readarr.Http;

namespace Readarr.Api.V1.ManualImport
{
    [V1ApiController]
    public class ManualImportController : Controller
    {
        private readonly IAuthorService _authorService;
        private readonly IBookService _bookService;
        private readonly IEditionService _editionService;
        private readonly IManualImportService _manualImportService;
        private readonly IMediaFileService _mediaFileService;
        private readonly IContributorEvidenceRepository _contributorEvidenceRepository;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public ManualImportController(IManualImportService manualImportService,
                                  IAuthorService authorService,
                                  IEditionService editionService,
                                  IBookService bookService,
                                  IMediaFileService mediaFileService,
                                  IContributorEvidenceRepository contributorEvidenceRepository,
                                  IConfigService configService,
                                  Logger logger)
        {
            _authorService = authorService;
            _bookService = bookService;
            _editionService = editionService;
            _manualImportService = manualImportService;
            _mediaFileService = mediaFileService;
            _contributorEvidenceRepository = contributorEvidenceRepository;
            _configService = configService;
            _logger = logger;
        }

        [HttpPost]
        public IActionResult UpdateItems([FromBody] List<ManualImportUpdateResource> resource)
        {
            return Accepted(UpdateImportItems(resource));
        }

        [HttpGet]
        public List<ManualImportResource> GetMediaFiles(string folder, string downloadId, int? authorId, bool filterExistingFiles = true, bool replaceExistingFiles = true)
        {
            NzbDrone.Core.Books.Author author = null;

            if (authorId > 0)
            {
                author = _authorService.GetAuthor(authorId.Value);
            }

            var filter = filterExistingFiles ? FilterFilesType.Matched : FilterFilesType.None;

            return AddManualImportEvidenceContext(_manualImportService.GetMediaFiles(folder, downloadId, author, filter, replaceExistingFiles).ToResource().Select(AddQualityWeight).ToList());
        }

        [HttpGet("paged")]
        public PagingResource<ManualImportResource> GetMediaFilesPaged([FromQuery] PagingRequestResource paging, string folder, string downloadId, int? authorId, bool filterExistingFiles = true, bool replaceExistingFiles = true)
        {
            NzbDrone.Core.Books.Author author = null;

            if (authorId > 0)
            {
                author = _authorService.GetAuthor(authorId.Value);
            }

            var filter = filterExistingFiles ? FilterFilesType.Matched : FilterFilesType.None;
            var page = paging?.Page ?? 1;
            var pageSize = paging?.PageSize ?? ManualImportService.MaxReviewPageSize;
            var pageResult = _manualImportService.GetMediaFilesPage(folder, downloadId, author, filter, replaceExistingFiles, page, pageSize);
            var records = AddManualImportEvidenceContext(pageResult.Records.ToResource().Select(AddQualityWeight).ToList());

            return new PagingResource<ManualImportResource>
            {
                Page = pageResult.Page,
                PageSize = pageResult.PageSize,
                SortKey = paging?.SortKey ?? "path",
                SortDirection = paging?.SortDirection ?? SortDirection.Ascending,
                TotalRecords = pageResult.TotalRecords,
                Records = records
            };
        }

        private ManualImportResource AddQualityWeight(ManualImportResource item)
        {
            if (item.Quality != null)
            {
                item.QualityWeight = Quality.DefaultQualityDefinitions.Single(q => q.Quality == item.Quality.Quality).Weight;
                item.QualityWeight += item.Quality.Revision.Real * 10;
                item.QualityWeight += item.Quality.Revision.Version;
            }

            return item;
        }

        private List<ManualImportResource> UpdateImportItems(List<ManualImportUpdateResource> resources)
        {
            var items = new List<ManualImportItem>();
            foreach (var resource in resources)
            {
                items.Add(new ManualImportItem
                {
                    Id = resource.Id,
                    Path = resource.Path,
                    Name = resource.Name,
                    Author = resource.AuthorId.HasValue ? _authorService.GetAuthor(resource.AuthorId.Value) : null,
                    Book = resource.BookId.HasValue ? _bookService.GetBook(resource.BookId.Value) : null,
                    Edition = resource.ForeignEditionId == null ? null : _editionService.GetEditionByForeignEditionId(resource.ForeignEditionId),
                    Quality = resource.Quality,
                    ReleaseGroup = resource.ReleaseGroup,
                    IndexerFlags = resource.IndexerFlags,
                    DownloadId = resource.DownloadId,
                    AdditionalFile = resource.AdditionalFile,
                    ReplaceExistingFiles = resource.ReplaceExistingFiles,
                    DisableReleaseSwitching = resource.DisableReleaseSwitching
                });
            }

            return AddManualImportEvidenceContext(_manualImportService.UpdateItems(items).Select(x => x.ToResource()).ToList());
        }

        private List<ManualImportResource> AddManualImportEvidenceContext(List<ManualImportResource> resources)
        {
            var paths = resources.Select(x => x.Path).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(PathEqualityComparer.Instance).ToList();
            var bookFiles = paths.Any() ? _mediaFileService.GetFileWithPath(paths) : new List<BookFile>();
            var bookFilesByPath = bookFiles
                .GroupBy(x => x.Path, PathEqualityComparer.Instance)
                .ToDictionary(x => x.Key, x => x.First(), PathEqualityComparer.Instance);
            var contributorEvidence = _contributorEvidenceRepository.GetByBookFileIds(bookFiles.Where(x => x.EditionId == 0).Select(x => x.Id))
                .GroupBy(x => x.BookFileId ?? 0)
                .ToDictionary(x => x.Key, x => x.Select(ContributorEvidenceResourceMapper.ToResource).ToList());

            foreach (var resource in resources)
            {
                resource.Review ??= new ManualImportReviewResource();

                bookFilesByPath.TryGetValue(resource.Path, out var bookFile);
                var evidence = bookFile != null && contributorEvidence.TryGetValue(bookFile.Id, out var fileEvidence) ? fileEvidence : new List<ContributorEvidenceResource>();

                ManualImportReviewResourceMapper.ApplyContributorEvidenceContext(resource.Review, bookFile, evidence);
                ManualImportReviewResourceMapper.ApplyMatchingCriteriaContext(resource.Review, _configService.MinimumBookMatchSimilarity);
            }

            return resources;
        }
    }
}
