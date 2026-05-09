using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MetadataSource;
using Readarr.Http;

namespace Readarr.Api.V1.Metadata
{
    [V1ApiController("metadata/compare")]
    public class MetadataComparisonController : Controller
    {
        private readonly IBookService _bookService;
        private readonly IEditionService _editionService;
        private readonly ISeriesBookLinkService _seriesBookLinkService;
        private readonly IContributorEvidenceRepository _contributorEvidenceRepository;
        private readonly IProvideBookInfo _bookInfo;
        private readonly IConfigService _configService;

        public MetadataComparisonController(IBookService bookService,
                                            IEditionService editionService,
                                            ISeriesBookLinkService seriesBookLinkService,
                                            IContributorEvidenceRepository contributorEvidenceRepository,
                                            IProvideBookInfo bookInfo,
                                            IConfigService configService)
        {
            _bookService = bookService;
            _editionService = editionService;
            _seriesBookLinkService = seriesBookLinkService;
            _contributorEvidenceRepository = contributorEvidenceRepository;
            _bookInfo = bookInfo;
            _configService = configService;
        }

        [HttpGet("book/{bookId:int}")]
        public ActionResult<MetadataComparisonResource> GetBookComparison(int bookId)
        {
            var book = _bookService.GetBook(bookId);
            var editions = _editionService.GetEditionsByBook(bookId);
            var localEdition = editions.SingleOrDefault(x => x.Monitored) ?? editions.FirstOrDefault();
            var metadataSource = _configService.MetadataSource.IsNullOrWhiteSpace() ? MetadataSourceConfig.LocalRReadingGlasses : _configService.MetadataSource;

            book.Editions = editions;
            book.SeriesLinks = _seriesBookLinkService.GetLinksByBook(new[] { bookId }.ToList());

            Book providerBook = null;
            Edition providerEdition = null;
            string providerError = null;

            if (book.ForeignBookId.IsNotNullOrWhiteSpace())
            {
                try
                {
                    var providerResult = _bookInfo.GetBookInfo(book.ForeignBookId);
                    providerBook = providerResult?.Item2;

                    if (providerBook != null)
                    {
                        providerBook.SeriesLinks = providerBook.SeriesLinks?.Value ?? new List<SeriesBookLink>();
                        providerEdition = SelectProviderEdition(providerBook, localEdition);
                    }
                }
                catch (Exception ex)
                {
                    providerError = $"Provider comparison metadata is unavailable: {ex.Message}";
                }
            }
            else
            {
                providerError = "Local book has no foreign book id to query provider metadata.";
            }

            var evidence = _contributorEvidenceRepository.GetByEditionIds(editions.Select(x => x.Id));

            return MetadataComparisonResourceMapper.ToResource(book, localEdition, providerBook, providerEdition, evidence, metadataSource, providerError);
        }

        private static Edition SelectProviderEdition(Book providerBook, Edition localEdition)
        {
            var providerEditions = providerBook?.Editions?.Value ?? new List<Edition>();

            if (!providerEditions.Any())
            {
                return null;
            }

            if (localEdition?.ForeignEditionId.IsNotNullOrWhiteSpace() == true)
            {
                var match = providerEditions.SingleOrDefault(x => x.ForeignEditionId == localEdition.ForeignEditionId);

                if (match != null)
                {
                    return match;
                }
            }

            return providerEditions.SingleOrDefault(x => x.Monitored) ?? providerEditions.First();
        }
    }
}
