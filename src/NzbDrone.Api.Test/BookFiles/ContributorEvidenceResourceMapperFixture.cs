using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using Readarr.Api.V1.BookFiles;

namespace NzbDrone.Api.Test.BookFiles
{
    [TestFixture]
    public class ContributorEvidenceResourceMapperFixture
    {
        [Test]
        public void should_add_narrator_discovery_context_for_review_evidence()
        {
            var resource = new ContributorEvidence
            {
                Role = "narrator",
                DisplayName = "Jane Reader",
                NormalizedName = "janereader",
                Source = "sttTranscript",
                Confidence = 82
            }.ToResource();

            resource.SourceLabel.Should().Be("STT transcript");
            resource.ConfidenceLabel.Should().Be("Medium-confidence review");
            resource.DiscoveryUrl.Should().Be("/narrators?term=Jane%20Reader");
            resource.ReviewOnlyReason.Should().Contain("does not write tags or metadata automatically");
        }

        [Test]
        public void should_keep_non_narrator_evidence_without_discovery_link()
        {
            var resource = new ContributorEvidence
            {
                Role = "author",
                DisplayName = "Alice Writer",
                Source = "manual"
            }.ToResource();

            resource.SourceLabel.Should().Be("Manual");
            resource.ConfidenceLabel.Should().Be("Manual evidence");
            resource.DiscoveryUrl.Should().BeNull();
            resource.ReviewOnlyReason.Should().BeNull();
        }
    }
}
