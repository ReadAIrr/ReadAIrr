using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.BookImport.Manual;
using NzbDrone.SignalR;
using NzbDrone.Test.Common;
using Readarr.Api.V1.BookFiles;
using Readarr.Api.V1.ManualImport;
using Readarr.Http;

namespace NzbDrone.Api.Test.BookFiles
{
    [TestFixture]
    public class BookFileControllerPagingFixture : TestBase
    {
        private Mock<IMediaFileService> _mediaFileService;
        private Mock<IDeleteMediaFiles> _mediaFileDeletionService;
        private Mock<IMetadataTagService> _metadataTagService;
        private Mock<IManualImportService> _manualImportService;
        private IManualImportReviewSessionCache _reviewSessionCache;
        private Mock<IAuthorService> _authorService;
        private Mock<IBookService> _bookService;
        private Mock<IUpgradableSpecification> _upgradableSpecification;
        private Mock<IUnmappedIdentificationSuggestionService> _unmappedIdentificationSuggestionService;
        private Mock<IAudioIntroTranscriptionService> _audioIntroTranscriptionService;
        private Mock<IContributorEvidenceRepository> _contributorEvidenceRepository;
        private Mock<IAudioTagEditService> _audioTagEditService;
        private Mock<IConfigService> _configService;
        private BookFileController _subject;

        [SetUp]
        public void SetUp()
        {
            _mediaFileService = new Mock<IMediaFileService>();
            _mediaFileDeletionService = new Mock<IDeleteMediaFiles>();
            _metadataTagService = new Mock<IMetadataTagService>();
            _manualImportService = new Mock<IManualImportService>();
            _reviewSessionCache = new ManualImportReviewSessionCache(NLog.LogManager.GetCurrentClassLogger());
            _authorService = new Mock<IAuthorService>();
            _bookService = new Mock<IBookService>();
            _upgradableSpecification = new Mock<IUpgradableSpecification>();
            _unmappedIdentificationSuggestionService = new Mock<IUnmappedIdentificationSuggestionService>();
            _audioIntroTranscriptionService = new Mock<IAudioIntroTranscriptionService>();
            _contributorEvidenceRepository = new Mock<IContributorEvidenceRepository>();
            _audioTagEditService = new Mock<IAudioTagEditService>();
            _configService = new Mock<IConfigService>();

            _manualImportService.Setup(x => x.GetMediaFiles(It.IsAny<List<string>>(), null, false))
                .Returns(new List<ManualImportItem>());
            _unmappedIdentificationSuggestionService.Setup(x => x.GetPersisted(It.IsAny<List<BookFileResource>>()))
                .Returns(new List<ManualImportIdentificationSuggestionResource>());
            _contributorEvidenceRepository.Setup(x => x.GetByBookFileIds(It.IsAny<IEnumerable<int>>()))
                .Returns(new List<ContributorEvidence>());
            _configService.SetupGet(x => x.MinimumBookMatchSimilarity).Returns(80);

            _subject = new BookFileController(
                new Mock<IBroadcastSignalRMessage>().Object,
                _mediaFileService.Object,
                _mediaFileDeletionService.Object,
                _metadataTagService.Object,
                _manualImportService.Object,
                _reviewSessionCache,
                _authorService.Object,
                _bookService.Object,
                _upgradableSpecification.Object,
                _unmappedIdentificationSuggestionService.Object,
                _audioIntroTranscriptionService.Object,
                _contributorEvidenceRepository.Object,
                _audioTagEditService.Object,
                _configService.Object);
        }

        [Test]
        public void paged_unmapped_endpoint_should_clamp_page_and_cap_page_size()
        {
            _mediaFileService.Setup(x => x.GetUnmappedFiles(It.Is<PagingSpec<BookFile>>(s => s.Page == 1 && s.PageSize == 500)))
                .Returns(new PagingSpec<BookFile>
                {
                    Page = 1,
                    PageSize = 500,
                    TotalRecords = 0,
                    Records = new List<BookFile>()
                });

            var result = _subject.GetUnmappedFilesPaged(new PagingRequestResource
            {
                Page = 0,
                PageSize = 9999,
                SortKey = "path",
                SortDirection = SortDirection.Descending
            });

            result.Page.Should().Be(1);
            result.PageSize.Should().Be(500);
            result.TotalRecords.Should().Be(0);
            result.SortKey.Should().BeNull();
            result.SortDirection.Should().Be(SortDirection.Default);
            result.Records.Should().BeEmpty();
        }

        [Test]
        public void paged_unmapped_endpoint_should_return_page_records_and_total_count()
        {
            _mediaFileService.Setup(x => x.GetUnmappedFiles(It.Is<PagingSpec<BookFile>>(s => s.Page == 2 && s.PageSize == 25)))
                .Returns(new PagingSpec<BookFile>
                {
                    Page = 2,
                    PageSize = 25,
                    TotalRecords = 42,
                    Records = new List<BookFile>
                    {
                        new BookFile
                        {
                            Id = 10,
                            EditionId = 0,
                            Edition = new LazyLoaded<Edition>(null),
                            Path = "/books/unmapped/a.m4b"
                        }
                    }
                });

            var result = _subject.GetUnmappedFilesPaged(new PagingRequestResource { Page = 2, PageSize = 25 });

            result.Page.Should().Be(2);
            result.PageSize.Should().Be(25);
            result.TotalRecords.Should().Be(42);
            result.Records.Should().ContainSingle();
            result.Records[0].Id.Should().Be(10);
            result.Records[0].Path.Should().Be("/books/unmapped/a.m4b");
            _manualImportService.Verify(x => x.GetMediaFiles(It.Is<List<string>>(paths => paths.Count == 1 && paths[0] == "/books/unmapped/a.m4b"), null, false), Times.Once);
        }

        [Test]
        public void existing_full_unmapped_endpoint_should_still_use_full_unmapped_service_path()
        {
            _mediaFileService.Setup(x => x.GetUnmappedFiles())
                .Returns(new List<BookFile>());

            var result = _subject.GetBookFiles(null, new List<int>(), new List<int>(), true);

            result.Should().BeEmpty();
            _mediaFileService.Verify(x => x.GetUnmappedFiles(), Times.Once);
            _mediaFileService.Verify(x => x.GetUnmappedFiles(It.IsAny<PagingSpec<BookFile>>()), Times.Never);
        }
    }
}
