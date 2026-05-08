using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.MediaFiles.BookImport.Manual;
using NzbDrone.Core.Parser.Model;
using Readarr.Api.V1.ManualImport;

namespace NzbDrone.Api.Test.ManualImport
{
    [TestFixture]
    public class ManualImportReviewResourceMapperFixture
    {
        [Test]
        public void should_shape_missing_candidate_reasons_from_tags()
        {
            var item = new ManualImportItem
            {
                Path = "/books/Alice Writer - The Hidden Book.m4b",
                Name = "Alice Writer - The Hidden Book",
                Tags = new ParsedTrackInfo
                {
                    Authors = new List<string> { "Alice Writer" },
                    BookTitle = "The Hidden Book",
                    Title = "The Hidden Book"
                },
                Rejections = new List<Rejection>()
            };

            var resource = item.ToReviewResource();

            resource.Status.Should().Be("noCandidate");
            resource.Parsed.Author.Should().Be("Alice Writer");
            resource.Parsed.Book.Should().Be("The Hidden Book");
            resource.Reasons.Should().Contain(x => x.Kind == "missingAuthor");
            resource.Reasons.Should().Contain(x => x.Kind == "missingBook");
        }

        [Test]
        public void should_surface_match_confidence_and_low_confidence_rejection()
        {
            var item = new ManualImportItem
            {
                Path = "/books/Alice Writer - The Hidden Book.m4b",
                Name = "Alice Writer - The Hidden Book",
                Tags = new ParsedTrackInfo(),
                MatchDistance = 0.42,
                MatchDistanceReasons = "book_title: 0.42",
                Rejections = new List<Rejection>
                {
                    new Rejection("Book match is not close enough: 58.0 % vs 80 %")
                }
            };

            var resource = item.ToReviewResource();

            resource.Confidence.Should().Be(58);
            resource.Status.Should().Be("noCandidate");
            resource.Reasons.Should().Contain(x => x.Kind == "lowConfidence");
            resource.Hints.Should().Contain(x => x.Kind == "matchDistance");
        }

        [Test]
        public void should_shape_no_edition_reason_when_book_candidate_has_no_edition()
        {
            var item = new ManualImportItem
            {
                Path = "/books/Alice Writer - The Hidden Book.m4b",
                Name = "Alice Writer - The Hidden Book",
                Author = new Author { Name = "Alice Writer" },
                Book = new Book { Title = "The Hidden Book" },
                Tags = new ParsedTrackInfo(),
                Rejections = new List<Rejection>()
            };

            var resource = item.ToReviewResource();

            resource.Status.Should().Be("noEdition");
            resource.StatusLabel.Should().Be("No edition");
            resource.Candidate.AuthorName.Should().Be("Alice Writer");
            resource.Candidate.BookTitle.Should().Be("The Hidden Book");
            resource.Reasons.Should().Contain(x => x.Kind == "noEdition");
        }
    }
}
