using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaFiles
{
    public interface IContributorEvidenceRepository : IBasicRepository<ContributorEvidence>
    {
        List<ContributorEvidence> GetByBookFileIds(IEnumerable<int> bookFileIds);
        List<ContributorEvidence> GetByEditionIds(IEnumerable<int> editionIds);
        List<ContributorEvidence> GetRecent(int take);
        void DeleteByBookFileIdsAndSources(IEnumerable<int> bookFileIds, IEnumerable<string> sources);
        void DeleteByBookFileIdSourceAndRole(int bookFileId, string source, string role);
    }

    public class ContributorEvidenceRepository : BasicRepository<ContributorEvidence>, IContributorEvidenceRepository
    {
        public ContributorEvidenceRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<ContributorEvidence> GetByBookFileIds(IEnumerable<int> bookFileIds)
        {
            var ids = bookFileIds.Distinct().ToList();

            if (!ids.Any())
            {
                return new List<ContributorEvidence>();
            }

            return Query(x => x.BookFileId.HasValue && ids.Contains(x.BookFileId.Value))
                .OrderBy(x => x.Role)
                .ThenBy(x => x.DisplayName)
                .ToList();
        }

        public List<ContributorEvidence> GetByEditionIds(IEnumerable<int> editionIds)
        {
            var ids = editionIds.Distinct().ToList();

            if (!ids.Any())
            {
                return new List<ContributorEvidence>();
            }

            return Query(x => x.EditionId.HasValue && ids.Contains(x.EditionId.Value))
                .OrderBy(x => x.Role)
                .ThenBy(x => x.DisplayName)
                .ToList();
        }

        public List<ContributorEvidence> GetRecent(int take)
        {
            if (take <= 0)
            {
                return new List<ContributorEvidence>();
            }

            var spec = new PagingSpec<ContributorEvidence>
            {
                Page = 1,
                PageSize = take,
                SortKey = "updated",
                SortDirection = SortDirection.Descending
            };

            return GetPaged(spec).Records;
        }

        public void DeleteByBookFileIdsAndSources(IEnumerable<int> bookFileIds, IEnumerable<string> sources)
        {
            var ids = bookFileIds.Distinct().ToList();
            var sourceList = sources.Where(x => x.IsNotNullOrWhiteSpace()).Distinct().ToList();

            if (!ids.Any() || !sourceList.Any())
            {
                return;
            }

            Delete(x => x.BookFileId.HasValue && ids.Contains(x.BookFileId.Value) && sourceList.Contains(x.Source));
        }

        public void DeleteByBookFileIdSourceAndRole(int bookFileId, string source, string role)
        {
            if (source.IsNullOrWhiteSpace() || role.IsNullOrWhiteSpace())
            {
                return;
            }

            Delete(x => x.BookFileId.HasValue && x.BookFileId.Value == bookFileId && x.Source == source && x.Role == role);
        }
    }
}
