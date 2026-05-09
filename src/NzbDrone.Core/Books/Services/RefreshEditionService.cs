using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Books
{
    public interface IRefreshEditionService
    {
        bool RefreshEditionInfo(List<Edition> add, List<Edition> update, List<Tuple<Edition, Edition>> merge, List<Edition> delete, List<Edition> upToDate, List<Edition> remoteEditions, bool forceUpdateFileTags);
    }

    public class RefreshEditionService : IRefreshEditionService
    {
        private readonly IEditionService _editionService;
        private readonly IMetadataTagService _metadataTagService;
        private readonly IContributorEvidenceRepository _contributorEvidenceRepository;
        private readonly Logger _logger;

        public RefreshEditionService(IEditionService editionService,
            IMetadataTagService metadataTagService,
            IContributorEvidenceRepository contributorEvidenceRepository,
            Logger logger)
        {
            _editionService = editionService;
            _metadataTagService = metadataTagService;
            _contributorEvidenceRepository = contributorEvidenceRepository;
            _logger = logger;
        }

        public bool RefreshEditionInfo(List<Edition> add, List<Edition> update, List<Tuple<Edition, Edition>> merge, List<Edition> delete, List<Edition> upToDate, List<Edition> remoteEditions, bool forceUpdateFileTags)
        {
            var updateList = new List<Edition>();

            // for editions that need updating, just grab the remote edition and set db ids
            foreach (var edition in update)
            {
                var remoteEdition = remoteEditions.Single(e => e.ForeignEditionId == edition.ForeignEditionId);
                edition.UseMetadataFrom(remoteEdition);

                // make sure title is not null
                edition.Title = edition.Title ?? "Unknown";
                updateList.Add(edition);
            }

            _editionService.DeleteMany(delete.Concat(merge.Select(x => x.Item1)).ToList());
            _editionService.UpdateMany(updateList);

            var tagsToUpdate = updateList;
            if (forceUpdateFileTags)
            {
                _logger.Debug("Forcing tag update due to Author/Book/Edition updates");
                tagsToUpdate = updateList.Concat(upToDate).ToList();
            }

            _metadataTagService.SyncTags(tagsToUpdate);
            SyncProviderContributorEvidence(add.Concat(updateList).Concat(upToDate).ToList(), remoteEditions);

            return add.Any() || delete.Any() || updateList.Any() || merge.Any();
        }

        private void SyncProviderContributorEvidence(List<Edition> editions, List<Edition> remoteEditions)
        {
            var editionsByForeignId = editions
                .Where(x => x.Id > 0 && x.ForeignEditionId.IsNotNullOrWhiteSpace())
                .GroupBy(x => x.ForeignEditionId)
                .ToDictionary(x => x.Key, x => x.First());

            if (!editionsByForeignId.Any())
            {
                return;
            }

            var evidence = remoteEditions
                .Where(x => x.ProviderContributorEvidence != null && x.ProviderContributorEvidence.Any())
                .SelectMany(remoteEdition =>
                {
                    if (!editionsByForeignId.TryGetValue(remoteEdition.ForeignEditionId, out var edition))
                    {
                        return new List<ContributorEvidence>();
                    }

                    return remoteEdition.ProviderContributorEvidence.Select(item => new ContributorEvidence
                    {
                        EditionId = edition.Id,
                        ForeignEditionId = edition.ForeignEditionId,
                        Role = item.Role,
                        DisplayName = item.DisplayName,
                        NormalizedName = item.NormalizedName,
                        Source = item.Source,
                        Confidence = item.Confidence,
                        RawValue = item.RawValue,
                        Created = DateTime.UtcNow,
                        Updated = DateTime.UtcNow
                    }).ToList();
                })
                .Where(x => x.DisplayName.IsNotNullOrWhiteSpace())
                .ToList();

            _contributorEvidenceRepository.DeleteByEditionIdsAndSources(editionsByForeignId.Values.Select(x => x.Id), new[] { "providerMetadata" });

            if (evidence.Any())
            {
                _contributorEvidenceRepository.InsertMany(evidence);
            }
        }
    }
}
