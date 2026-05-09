using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;
using Readarr.Api.V1.Contributors;

namespace NzbDrone.Api.Test.Contributors
{
    [TestFixture]
    public class NarratorEvidenceResourceMapperFixture
    {
        [Test]
        public void should_group_narrator_evidence_by_normalized_name()
        {
            var now = DateTime.UtcNow;
            var evidence = new List<ContributorEvidence>
            {
                new ContributorEvidence
                {
                    BookFileId = 1,
                    Role = "narrator",
                    DisplayName = "Jane Reader",
                    NormalizedName = "janereader",
                    Source = "manual",
                    Updated = now.AddMinutes(-1)
                },
                new ContributorEvidence
                {
                    BookFileId = 2,
                    Role = "narrator",
                    DisplayName = "Jane Reader",
                    NormalizedName = "janereader",
                    Source = "sttTranscript",
                    Confidence = 82,
                    Updated = now
                },
                new ContributorEvidence
                {
                    BookFileId = 3,
                    Role = "editor",
                    DisplayName = "Jane Reader",
                    NormalizedName = "janereader",
                    Source = "manual",
                    Updated = now
                }
            };

            var bookFiles = new List<BookFile>
            {
                new BookFile { Id = 1, Path = "/books/one.m4b" },
                new BookFile { Id = 2, Path = "/books/two.m4b" }
            };

            var result = NarratorEvidenceResourceMapper.ToResource(evidence, bookFiles);

            result.Should().HaveCount(1);
            result[0].DisplayName.Should().Be("Jane Reader");
            result[0].EvidenceCount.Should().Be(2);
            result[0].WorkCount.Should().Be(2);
            result[0].UnmappedFileCount.Should().Be(2);
            result[0].ManualEvidenceCount.Should().Be(1);
            result[0].ReviewEvidenceCount.Should().Be(1);
            result[0].IsCanonicalIdentity.Should().BeFalse();
            result[0].ReviewOnlyReason.Should().Contain("not provider-confirmed");
            result[0].SourceCounts.Should().Contain(x => x.Source == "manual" && x.Count == 1);
            result[0].SourceCounts.Should().Contain(x => x.Source == "sttTranscript" && x.Count == 1);
            result[0].Examples.Should().Contain(x => x.BookFileId == 2 && x.Path == "/books/two.m4b" && x.Confidence == 82);
        }

        [Test]
        public void should_filter_by_term_and_source()
        {
            var now = DateTime.UtcNow;
            var evidence = new List<ContributorEvidence>
            {
                new ContributorEvidence { Role = "narrator", DisplayName = "Jane Reader", NormalizedName = "janereader", Source = "manual", Updated = now },
                new ContributorEvidence { Role = "narrator", DisplayName = "Robin Voice", NormalizedName = "robinvoice", Source = "aiReview", Updated = now }
            };

            var result = NarratorEvidenceResourceMapper.ToResource(evidence, new List<BookFile>(), "voice", "aiReview");

            result.Should().HaveCount(1);
            result[0].DisplayName.Should().Be("Robin Voice");
        }

        [Test]
        public void should_group_alias_evidence_under_user_standardized_canonical_name()
        {
            var now = DateTime.UtcNow;
            var evidence = new List<ContributorEvidence>
            {
                new ContributorEvidence { Role = "narrator", DisplayName = "Jane Reader", NormalizedName = "janereader", Source = "manual", Updated = now },
                new ContributorEvidence { Role = "narrator", DisplayName = "J. Reader", NormalizedName = "jreader", Source = "sttTranscript", Updated = now.AddMinutes(-1) }
            };
            var aliases = new List<NarratorIdentityLink>
            {
                new NarratorIdentityLink
                {
                    Id = 5,
                    CanonicalName = "Jane Reader",
                    CanonicalNormalizedName = "janereader",
                    AliasName = "J. Reader",
                    AliasNormalizedName = "jreader",
                    RelationshipType = "alias",
                    DisplayPreference = "canonical"
                }
            };

            var result = NarratorEvidenceResourceMapper.ToResource(evidence, new List<BookFile>(), aliases);

            result.Should().HaveCount(1);
            result[0].DisplayName.Should().Be("Jane Reader");
            result[0].NormalizedName.Should().Be("janereader");
            result[0].EvidenceCount.Should().Be(2);
            result[0].IsCanonicalIdentity.Should().BeTrue();
            result[0].ReviewOnlyReason.Should().Contain("user-standardized");
            result[0].AliasCount.Should().Be(1);
            result[0].Aliases.Should().ContainSingle(x => x.Id == 5 && x.AliasName == "J. Reader");
        }

        [Test]
        public void should_return_canonical_detail_when_alias_normalized_name_is_requested()
        {
            var now = DateTime.UtcNow;
            var evidence = new List<ContributorEvidence>
            {
                new ContributorEvidence { Role = "narrator", DisplayName = "Jane Reader", NormalizedName = "janereader", Source = "manual", Updated = now },
                new ContributorEvidence { Role = "narrator", DisplayName = "J. Reader", NormalizedName = "jreader", Source = "sttTranscript", Updated = now.AddMinutes(-1) }
            };
            var aliases = new List<NarratorIdentityLink>
            {
                new NarratorIdentityLink
                {
                    Id = 5,
                    CanonicalName = "Jane Reader",
                    CanonicalNormalizedName = "janereader",
                    AliasName = "J. Reader",
                    AliasNormalizedName = "jreader",
                    RelationshipType = "alias",
                    DisplayPreference = "canonical"
                }
            };

            var result = NarratorEvidenceResourceMapper.ToDetailResource(evidence, new List<BookFile>(), aliases, "jreader");

            result.DisplayName.Should().Be("Jane Reader");
            result.NormalizedName.Should().Be("janereader");
            result.Works.Should().HaveCount(2);
            result.Aliases.Should().ContainSingle(x => x.AliasNormalizedName == "jreader");
        }

        [Test]
        public void should_shape_detail_rows_with_book_author_and_edition_context()
        {
            var now = DateTime.UtcNow;
            var author = new Author { Id = 7, Name = "Alice Writer" };
            author.Metadata.Value.TitleSlug = "alice-writer";
            var book = new Book { Id = 8, Title = "The Hidden Book", TitleSlug = "the-hidden-book", Author = author };
            var edition = new Edition { Id = 9, Title = "The Hidden Book Audio", Book = book };
            var file = new BookFile
            {
                Id = 10,
                Path = "/books/alice/the-hidden-book.m4b",
                EditionId = edition.Id,
                Edition = edition,
                Author = author
            };

            var evidence = new List<ContributorEvidence>
            {
                new ContributorEvidence
                {
                    Id = 11,
                    BookFileId = file.Id,
                    EditionId = edition.Id,
                    Role = "narrator",
                    DisplayName = "Jane Reader",
                    NormalizedName = "janereader",
                    Source = "manual",
                    Confidence = 100,
                    Updated = now
                }
            };

            var result = NarratorEvidenceResourceMapper.ToDetailResource(evidence, new List<BookFile> { file }, "janereader");

            result.DisplayName.Should().Be("Jane Reader");
            result.MatchedBookCount.Should().Be(1);
            result.UnmappedFileCount.Should().Be(0);
            result.ManualEvidenceCount.Should().Be(1);
            result.IsCanonicalIdentity.Should().BeFalse();
            result.Works.Should().HaveCount(1);
            result.Works[0].BookTitle.Should().Be("The Hidden Book");
            result.Works[0].BookTitleSlug.Should().Be("the-hidden-book");
            result.Works[0].AuthorName.Should().Be("Alice Writer");
            result.Works[0].AuthorTitleSlug.Should().Be("alice-writer");
            result.Works[0].EditionTitle.Should().Be("The Hidden Book Audio");
            result.Works[0].Path.Should().Be("/books/alice/the-hidden-book.m4b");
        }
    }
}
