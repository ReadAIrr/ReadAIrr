using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.BookImport.Manual;
using NzbDrone.Core.Parser.Model;
using Readarr.Api.V1.BookFiles;
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
        public void should_surface_current_matching_criteria_context()
        {
            var resource = new ManualImportReviewResource
            {
                Confidence = 72,
                Hints = new List<ManualImportReviewReasonResource>
                {
                    new ManualImportReviewReasonResource
                    {
                        Kind = "matchDistance",
                        Label = "Match distance",
                        Detail = "book_title: 0.28"
                    }
                }
            };

            ManualImportReviewResourceMapper.ApplyMatchingCriteriaContext(resource, 80);

            resource.MinimumMatchSimilarity.Should().Be(80);
            resource.Hints.Should().Contain(x => x.Kind == "matchingCriteria" && x.Detail.Contains("72% match confidence"));
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

        [Test]
        public void should_warn_when_contributor_evidence_sources_disagree_on_narrator()
        {
            var resource = new ManualImportReviewResource
            {
                Hints = new List<ManualImportReviewReasonResource>(),
                Candidate = new ManualImportCandidateResource
                {
                    AuthorName = "Alice Writer",
                    BookTitle = "The Hidden Book"
                },
                ContributorEvidence = new List<ContributorEvidenceResource>
                {
                    new ContributorEvidenceResource
                    {
                        Role = "narrator",
                        DisplayName = "Jane Reader",
                        NormalizedName = "janereader",
                        Source = "manual"
                    },
                    new ContributorEvidenceResource
                    {
                        Role = "narrator",
                        DisplayName = "Janet Voice",
                        NormalizedName = "janetvoice",
                        Source = "sttTranscript"
                    }
                },
                Suggestions = new List<ManualImportIdentificationSuggestionResource>
                {
                    new ManualImportIdentificationSuggestionResource
                    {
                        Type = "deepAudio",
                        Provider = "openrouter-stt",
                        Narrator = "Janet Voice"
                    }
                }
            };

            ManualImportReviewResourceMapper.ApplySuggestionEvidence(resource);

            resource.Hints.Should().Contain(x => x.Kind == "narratorEvidenceConflict");
            resource.Suggestions[0].Warnings.Should().Contain(x => x.Kind == "narratorEvidenceMismatch");
        }

        [Test]
        public void should_allow_manual_evidence_editing_when_manual_import_row_has_durable_unmapped_book_file()
        {
            var resource = new ManualImportReviewResource
            {
                Hints = new List<ManualImportReviewReasonResource>(),
                Suggestions = new List<ManualImportIdentificationSuggestionResource>()
            };

            ManualImportReviewResourceMapper.ApplyContributorEvidenceContext(resource,
                new BookFile { Id = 12, EditionId = 0 },
                new List<ContributorEvidenceResource>
                {
                    new ContributorEvidenceResource
                    {
                        BookFileId = 12,
                        Role = "narrator",
                        DisplayName = "Jane Reader",
                        NormalizedName = "janereader",
                        Source = "manual"
                    }
                });

            resource.BookFileId.Should().Be(12);
            resource.CanEditContributorEvidence.Should().BeTrue();
            resource.ContributorEvidenceEditReason.Should().Contain("persisted unmapped file");
            resource.ContributorEvidence.Should().ContainSingle(x => x.DisplayName == "Jane Reader");
        }

        [Test]
        public void should_keep_manual_import_path_hash_rows_read_only_without_durable_unmapped_book_file()
        {
            var resource = new ManualImportReviewResource
            {
                Hints = new List<ManualImportReviewReasonResource>(),
                Suggestions = new List<ManualImportIdentificationSuggestionResource>()
            };

            ManualImportReviewResourceMapper.ApplyContributorEvidenceContext(resource, null, new List<ContributorEvidenceResource>());

            resource.BookFileId.Should().BeNull();
            resource.CanEditContributorEvidence.Should().BeFalse();
            resource.ContributorEvidenceEditReason.Should().Contain("path-only");
        }

        [Test]
        public void should_not_allow_manual_evidence_editing_for_already_matched_book_file()
        {
            var resource = new ManualImportReviewResource
            {
                Hints = new List<ManualImportReviewReasonResource>(),
                Suggestions = new List<ManualImportIdentificationSuggestionResource>()
            };

            ManualImportReviewResourceMapper.ApplyContributorEvidenceContext(resource, new BookFile { Id = 12, EditionId = 33 }, new List<ContributorEvidenceResource>());

            resource.BookFileId.Should().BeNull();
            resource.CanEditContributorEvidence.Should().BeFalse();
            resource.ContributorEvidenceEditReason.Should().Contain("unmapped files");
        }
    }
}
