using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
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
    }
}
