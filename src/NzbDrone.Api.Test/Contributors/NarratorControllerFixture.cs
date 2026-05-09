using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Test.Common;
using Readarr.Api.V1.Contributors;
using Readarr.Http;

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
        public void paged_list_should_use_query_scoped_narrator_evidence_read()
        {
            var evidence = new List<ContributorEvidence>
                {
                    new ContributorEvidence
                    {
                        Id = 1,
                        BookFileId = 10,
                        Role = "narrator",
                        DisplayName = "Jane Reader",
                        NormalizedName = "janereader",
                        Source = "manual",
                        Updated = DateTime.UtcNow
                    }
                };

            _contributorEvidenceRepository.Setup(x => x.GetNarratorEvidenceForIdentityIndex("jane", "manual"))
                .Returns(evidence);
            _narratorIdentityLinkRepository.Setup(x => x.All())
                .Returns(new List<NarratorIdentityLink>().AsQueryable());
            _mediaFileService.Setup(x => x.Get(It.IsAny<IEnumerable<int>>()))
                .Returns(new List<BookFile> { new BookFile { Id = 10, Path = "/books/jane.m4b" } });

            var result = _subject.GetNarratorEvidencePaged(new PagingRequestResource { Page = 2, PageSize = 25 }, "jane", "manual");

            result.Page.Should().Be(2);
            result.PageSize.Should().Be(25);
            result.TotalRecords.Should().Be(1);
            result.Records.Should().BeEmpty();
            _contributorEvidenceRepository.Verify(x => x.All(), Times.Never);
            _contributorEvidenceRepository.Verify(x => x.GetNarratorEvidenceForIdentityIndex("jane", "manual"), Times.Once);
        }

        [Test]
        public void paged_list_should_page_grouped_narrator_identities_not_raw_evidence_rows()
        {
            var now = DateTime.UtcNow;
            var evidence = new List<ContributorEvidence>
            {
                new ContributorEvidence { Id = 1, Role = "narrator", DisplayName = "Jane Reader", NormalizedName = "janereader", Source = "manual", Updated = now },
                new ContributorEvidence { Id = 2, Role = "narrator", DisplayName = "Jane Reader", NormalizedName = "janereader", Source = "sttTranscript", Updated = now.AddMinutes(-1) },
                new ContributorEvidence { Id = 3, Role = "narrator", DisplayName = "Robin Voice", NormalizedName = "robinvoice", Source = "providerMetadata", Updated = now.AddMinutes(-2) }
            };

            _contributorEvidenceRepository.Setup(x => x.GetNarratorEvidenceForIdentityIndex(null, null))
                .Returns(evidence);
            _narratorIdentityLinkRepository.Setup(x => x.All())
                .Returns(new List<NarratorIdentityLink>().AsQueryable());

            var result = _subject.GetNarratorEvidencePaged(new PagingRequestResource { Page = 1, PageSize = 1 }, null, null);

            result.TotalRecords.Should().Be(2);
            result.Records.Should().ContainSingle();
            result.Records[0].DisplayName.Should().Be("Jane Reader");
            result.Records[0].EvidenceCount.Should().Be(2);
            result.Records[0].HasProviderConfirmedEvidence.Should().BeFalse();
        }

        [Test]
        public void detail_should_use_alias_scoped_narrator_evidence_read()
        {
            var aliases = new List<NarratorIdentityLink>
            {
                new NarratorIdentityLink
                {
                    CanonicalName = "Jane Reader",
                    CanonicalNormalizedName = "janereader",
                    AliasName = "J. Reader",
                    AliasNormalizedName = "jreader"
                }
            };

            _narratorIdentityLinkRepository.Setup(x => x.All())
                .Returns(aliases.AsQueryable());
            _narratorIdentityLinkRepository.Setup(x => x.GetByNormalizedName("jreader"))
                .Returns(aliases);
            _contributorEvidenceRepository.Setup(x => x.GetNarratorEvidenceByNames(It.Is<IEnumerable<string>>(names => names.Contains("janereader") && names.Contains("jreader")), null))
                .Returns(new List<ContributorEvidence>
                {
                    new ContributorEvidence
                    {
                        Id = 2,
                        Role = "narrator",
                        DisplayName = "J. Reader",
                        NormalizedName = "jreader",
                        Source = "sttTranscript",
                        Updated = DateTime.UtcNow
                    }
                });

            var result = _subject.GetNarratorEvidenceDetail("jreader");

            result.Value.DisplayName.Should().Be("Jane Reader");
            result.Value.EvidenceCount.Should().Be(1);
            _contributorEvidenceRepository.Verify(x => x.All(), Times.Never);
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
