using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.BookTests
{
    [TestFixture]
    public class RefreshEditionServiceFixture : CoreTest<RefreshEditionService>
    {
        [Test]
        public void should_persist_provider_contributor_evidence_for_refreshed_editions()
        {
            var localEdition = new Edition
            {
                Id = 12,
                ForeignEditionId = "987",
                Title = "Local Edition"
            };

            var remoteEdition = new Edition
            {
                ForeignEditionId = "987",
                Title = "Remote Edition",
                ProviderContributorEvidence = new List<ContributorEvidence>
                {
                    new ContributorEvidence
                    {
                        ForeignEditionId = "987",
                        Role = "narrator",
                        DisplayName = "Provider Reader",
                        NormalizedName = "providerreader",
                        Source = "providerMetadata",
                        Confidence = 95,
                        RawValue = "providerRole=narrator;foreignContributorId=44;foreignEditionId=987"
                    }
                }
            };

            Subject.RefreshEditionInfo(
                new List<Edition>(),
                new List<Edition> { localEdition },
                new List<Tuple<Edition, Edition>>(),
                new List<Edition>(),
                new List<Edition>(),
                new List<Edition> { remoteEdition },
                false);

            Mocker.GetMock<IContributorEvidenceRepository>()
                .Verify(x => x.DeleteByEditionIdsAndSources(It.Is<IEnumerable<int>>(ids => ids.Contains(localEdition.Id)), It.Is<IEnumerable<string>>(sources => sources.Contains("providerMetadata"))), Times.Once);

            Mocker.GetMock<IContributorEvidenceRepository>()
                .Verify(x => x.InsertMany(It.Is<IList<ContributorEvidence>>(items =>
                    items.Count == 1 &&
                    items[0].EditionId == localEdition.Id &&
                    items[0].ForeignEditionId == localEdition.ForeignEditionId &&
                    items[0].Role == "narrator" &&
                    items[0].DisplayName == "Provider Reader" &&
                    items[0].Source == "providerMetadata")), Times.Once);
        }
    }
}
