using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Test.Common;
using Readarr.Api.V1.Contributors;

namespace NzbDrone.Api.Test.Contributors
{
    [TestFixture]
    public class NarratorControllerFixture : TestBase
    {
        private Mock<IContributorEvidenceRepository> _contributorEvidenceRepository;
        private Mock<INarratorIdentityLinkRepository> _narratorIdentityLinkRepository;
        private Mock<IMediaFileService> _mediaFileService;
        private NarratorController _subject;

        [SetUp]
        public void SetUp()
        {
            _contributorEvidenceRepository = new Mock<IContributorEvidenceRepository>();
            _narratorIdentityLinkRepository = new Mock<INarratorIdentityLinkRepository>();
            _mediaFileService = new Mock<IMediaFileService>();

            _subject = new NarratorController(
                _contributorEvidenceRepository.Object,
                _narratorIdentityLinkRepository.Object,
                _mediaFileService.Object);
        }

        [Test]
        public void link_should_reject_self_link()
        {
            var result = _subject.LinkNarratorIdentity(new NarratorIdentityLinkUpdateResource
            {
                CanonicalName = "Jane Reader",
                AliasName = "Jane Reader"
            });

            var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.Value.Should().Be("Canonical and alias narrator names must be different.");
        }

        [Test]
        public void link_should_reject_duplicate_alias_link()
        {
            _narratorIdentityLinkRepository.Setup(x => x.FindByAlias("jreader"))
                .Returns(new NarratorIdentityLink
                {
                    CanonicalName = "Jane Reader",
                    CanonicalNormalizedName = "janereader",
                    AliasName = "J. Reader",
                    AliasNormalizedName = "jreader"
                });

            var result = _subject.LinkNarratorIdentity(new NarratorIdentityLinkUpdateResource
            {
                CanonicalName = "Jane Reader",
                AliasName = "J. Reader"
            });

            var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.Value.Should().Be("This narrator identity is already linked. Unlink it before creating a different link.");
        }
    }
}
