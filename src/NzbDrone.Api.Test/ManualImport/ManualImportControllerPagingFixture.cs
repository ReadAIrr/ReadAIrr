using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.BookImport.Manual;
using NzbDrone.Core.Qualities;
using NzbDrone.Test.Common;
using Readarr.Api.V1.ManualImport;
using Readarr.Http;

namespace NzbDrone.Api.Test.ManualImport
{
    [TestFixture]
    public class ManualImportControllerPagingFixture : TestBase
    {
        private Mock<IManualImportService> _manualImportService;
        private Mock<IAuthorService> _authorService;
        private Mock<IEditionService> _editionService;
        private Mock<IBookService> _bookService;
        private Mock<IMediaFileService> _mediaFileService;
        private Mock<IContributorEvidenceRepository> _contributorEvidenceRepository;
        private Mock<IConfigService> _configService;
        private ManualImportController _subject;

        [SetUp]
        public void SetUp()
        {
            _manualImportService = new Mock<IManualImportService>();
            _authorService = new Mock<IAuthorService>();
            _editionService = new Mock<IEditionService>();
            _bookService = new Mock<IBookService>();
            _mediaFileService = new Mock<IMediaFileService>();
            _contributorEvidenceRepository = new Mock<IContributorEvidenceRepository>();
            _configService = new Mock<IConfigService>();

            _mediaFileService.Setup(x => x.GetFileWithPath(It.IsAny<List<string>>())).Returns(new List<BookFile>());
            _contributorEvidenceRepository.Setup(x => x.GetByBookFileIds(It.IsAny<IEnumerable<int>>())).Returns(new List<ContributorEvidence>());
            _configService.SetupGet(x => x.MinimumBookMatchSimilarity).Returns(80);

            _subject = new ManualImportController(
                _manualImportService.Object,
                _authorService.Object,
                _editionService.Object,
                _bookService.Object,
                _mediaFileService.Object,
                _contributorEvidenceRepository.Object,
                _configService.Object,
                LogManager.GetCurrentClassLogger());
        }

        [Test]
        public void paged_endpoint_should_return_page_metadata_and_records_without_server_sort_claim()
        {
            _manualImportService.Setup(x => x.GetMediaFilesPage("/downloads/books", null, null, FilterFilesType.Matched, true, 2, 25))
                .Returns(new ManualImportPageResult
                {
                    Page = 2,
                    PageSize = 25,
                    TotalRecords = 42,
                    Records = new List<ManualImportItem>
                    {
                        new ManualImportItem
                        {
                            Id = 10,
                            Path = "/downloads/books/a.mp3",
                            Name = "a",
                            Size = 1024,
                            Quality = new QualityModel(Quality.MP3)
                        }
                    }
                });

            var result = _subject.GetMediaFilesPaged(new PagingRequestResource
            {
                Page = 2,
                PageSize = 25,
                SortKey = "path",
                SortDirection = SortDirection.Descending
            }, "/downloads/books", null, null);

            result.Page.Should().Be(2);
            result.PageSize.Should().Be(25);
            result.TotalRecords.Should().Be(42);
            result.SortKey.Should().BeNull();
            result.SortDirection.Should().Be(SortDirection.Default);
            result.Records.Should().ContainSingle();
            result.Records[0].Path.Should().Be("/downloads/books/a.mp3");
        }

        [Test]
        public void existing_full_endpoint_should_still_use_full_manual_import_service_path()
        {
            _manualImportService.Setup(x => x.GetMediaFiles("/downloads/books", null, null, FilterFilesType.Matched, true))
                .Returns(new List<ManualImportItem>());

            var result = _subject.GetMediaFiles("/downloads/books", null, null);

            result.Should().BeEmpty();
            _manualImportService.Verify(x => x.GetMediaFiles("/downloads/books", null, null, FilterFilesType.Matched, true), Times.Once);
            _manualImportService.Verify(x => x.GetMediaFilesPage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Author>(), It.IsAny<FilterFilesType>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }
    }
}
