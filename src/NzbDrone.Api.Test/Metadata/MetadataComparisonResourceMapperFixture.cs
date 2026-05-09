using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;
using Readarr.Api.V1.Metadata;

namespace NzbDrone.Api.Test.Metadata
{
    [TestFixture]
    public class MetadataComparisonResourceMapperFixture
    {
        [Test]
        public void should_classify_confirmed_missing_conflicting_and_provider_only_fields()
        {
            var localBook = Book("local-title", "Local Title", "Alice Author");
            var providerBook = Book("local-title", "Provider Title", "Alice Author");
            var localEdition = new Edition
            {
                Id = 10,
                BookId = localBook.Id,
                Title = "Local Audio",
                Language = "eng",
                Isbn13 = null,
                Publisher = "Local Publisher",
                ReleaseDate = new DateTime(2020, 1, 2),
                PageCount = 300
            };
            var providerEdition = new Edition
            {
                Id = 11,
                BookId = providerBook.Id,
                Title = "Local Audio",
                Language = "eng",
                Isbn13 = "9781234567890",
                Publisher = "Provider Publisher",
                ReleaseDate = new DateTime(2020, 1, 2),
                PageCount = 300
            };

            var result = MetadataComparisonResourceMapper.ToResource(localBook, localEdition, providerBook, providerEdition, new List<ContributorEvidence>(), "metadata-source");

            result.ProviderAvailable.Should().BeTrue();
            result.SummaryStatus.Should().Be("needs-review");
            result.Fields.Should().Contain(x => x.Field == "author" && x.Status == "confirmed");
            result.Fields.Should().Contain(x => x.Field == "title" && x.Status == "conflicting");
            result.Fields.Should().Contain(x => x.Field == "isbn13" && x.Status == "provider-only");
            result.Fields.Should().Contain(x => x.Field == "publisher" && x.Status == "conflicting");
        }

        [Test]
        public void should_keep_provider_unavailable_as_review_only_without_throwing()
        {
            var localBook = Book("local-title", "Local Title", "Alice Author");
            var localEdition = new Edition
            {
                Id = 10,
                BookId = localBook.Id,
                Title = "Local Audio"
            };

            var result = MetadataComparisonResourceMapper.ToResource(localBook, localEdition, null, null, new List<ContributorEvidence>(), "metadata-source", "Provider unavailable");

            result.ProviderAvailable.Should().BeFalse();
            result.SummaryStatus.Should().Be("needs-review");
            result.Fields.Should().Contain(x => x.Field == "providerAvailability" && x.Status == "needs-review");
            result.Fields.Should().Contain(x => x.Field == "title" && x.Status == "local-only");
        }

        [Test]
        public void should_classify_narrator_evidence_without_claiming_canonical_truth()
        {
            var localBook = Book("local-title", "Local Title", "Alice Author");
            var providerBook = Book("local-title", "Local Title", "Alice Author");
            var edition = new Edition
            {
                Id = 10,
                BookId = localBook.Id,
                Title = "Local Audio"
            };
            var evidence = new List<ContributorEvidence>
            {
                new ContributorEvidence { EditionId = edition.Id, Role = "narrator", DisplayName = "Jane Reader", Source = "manual", Confidence = 100 },
                new ContributorEvidence { EditionId = edition.Id, Role = "narrator", DisplayName = "Different Voice", Source = "providerMetadata", Confidence = 90 }
            };

            var result = MetadataComparisonResourceMapper.ToResource(localBook, edition, providerBook, edition, evidence, "metadata-source");

            result.Fields.Should().Contain(x => x.Field == "narrators" &&
                                                x.Status == "conflicting" &&
                                                x.Explanation.Contains("review-only"));
        }

        private static Book Book(string foreignBookId, string title, string authorName)
        {
            return new Book
            {
                Id = 1,
                ForeignBookId = foreignBookId,
                Title = title,
                AuthorMetadata = new AuthorMetadata
                {
                    Name = authorName
                },
                Editions = new List<Edition>(),
                SeriesLinks = new List<SeriesBookLink>()
            };
        }
    }
}
