using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MetadataSource;
using Readarr.Api.V1.Metadata;

namespace NzbDrone.Api.Test.Metadata
{
    [TestFixture]
    public class MetadataComparisonControllerFixture
    {
        private Mock<IBookService> _bookService;
        private Mock<IEditionService> _editionService;
        private Mock<ISeriesBookLinkService> _seriesBookLinkService;
        private Mock<IContributorEvidenceRepository> _contributorEvidenceRepository;
        private Mock<IProvideBookInfo> _bookInfo;
        private Mock<IConfigService> _configService;
        private MetadataComparisonController _subject;

        [SetUp]
        public void SetUp()
        {
            _bookService = new Mock<IBookService>();
            _editionService = new Mock<IEditionService>();
            _seriesBookLinkService = new Mock<ISeriesBookLinkService>();
            _contributorEvidenceRepository = new Mock<IContributorEvidenceRepository>();
            _bookInfo = new Mock<IProvideBookInfo>();
            _configService = new Mock<IConfigService>();

            _subject = new MetadataComparisonController(
                _bookService.Object,
                _editionService.Object,
                _seriesBookLinkService.Object,
                _contributorEvidenceRepository.Object,
                _bookInfo.Object,
                _configService.Object);
        }

        [Test]
        public void should_return_review_resource_when_provider_metadata_is_unavailable_without_writing_metadata()
        {
            var book = new Book
            {
                Id = 5,
                ForeignBookId = "book-5",
                Title = "Local Book",
                AuthorMetadata = new AuthorMetadata { Name = "Alice Author" },
                Editions = new List<Edition>(),
                SeriesLinks = new List<SeriesBookLink>()
            };
            var edition = new Edition
            {
                Id = 7,
                BookId = book.Id,
                Title = "Local Edition",
                Monitored = true
            };

            _bookService.Setup(x => x.GetBook(book.Id)).Returns(book);
            _editionService.Setup(x => x.GetEditionsByBook(book.Id)).Returns(new List<Edition> { edition });
            _seriesBookLinkService.Setup(x => x.GetLinksByBook(It.IsAny<List<int>>())).Returns(new List<SeriesBookLink>());
            _contributorEvidenceRepository.Setup(x => x.GetByEditionIds(It.IsAny<IEnumerable<int>>())).Returns(new List<ContributorEvidence>());
            _configService.SetupGet(x => x.MetadataSource).Returns("https://metadata.example.test");
            _bookInfo.Setup(x => x.GetBookInfo(book.ForeignBookId)).Throws(new Exception("offline"));

            var result = _subject.GetBookComparison(book.Id).Value;

            result.ProviderAvailable.Should().BeFalse();
            result.SummaryStatus.Should().Be("needs-review");
            result.Fields.Should().Contain(x => x.Field == "providerAvailability");
            _bookService.Verify(x => x.UpdateBook(It.IsAny<Book>()), Times.Never);
            _editionService.Verify(x => x.UpdateMany(It.IsAny<List<Edition>>()), Times.Never);
            _contributorEvidenceRepository.Verify(x => x.DeleteByEditionIdsAndSources(It.IsAny<IEnumerable<int>>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        }
    }
}
