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
        List<ContributorEvidence> GetByForeignEditionIds(IEnumerable<string> foreignEditionIds);
        PagingSpec<ContributorEvidence> GetNarratorEvidence(PagingSpec<ContributorEvidence> pagingSpec, string term, string source);
        List<ContributorEvidence> GetNarratorEvidenceForIdentityIndex(string term, string source);
        List<ContributorEvidence> GetNarratorEvidenceByNames(IEnumerable<string> normalizedNames, string source = null);
        List<ContributorEvidence> GetRecent(int take);
        void DeleteByBookFileIdsAndSources(IEnumerable<int> bookFileIds, IEnumerable<string> sources);
        void DeleteByBookFileIdSourceAndRole(int bookFileId, string source, string role);
        void DeleteByEditionIdsAndSources(IEnumerable<int> editionIds, IEnumerable<string> sources);
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

        public List<ContributorEvidence> GetByForeignEditionIds(IEnumerable<string> foreignEditionIds)
        {
            var ids = foreignEditionIds.Where(x => x.IsNotNullOrWhiteSpace()).Distinct().ToList();

            if (!ids.Any())
            {
                return new List<ContributorEvidence>();
            }

            return Query(x => x.ForeignEditionId != null && ids.Contains(x.ForeignEditionId))
                .OrderBy(x => x.Role)
                .ThenBy(x => x.DisplayName)
                .ToList();
        }

        public PagingSpec<ContributorEvidence> GetNarratorEvidence(PagingSpec<ContributorEvidence> pagingSpec, string term, string source)
        {
            pagingSpec.FilterExpressions.Add(x => x.Role == "narrator" && x.DisplayName != null && x.DisplayName != string.Empty);

            if (source.IsNotNullOrWhiteSpace())
            {
                pagingSpec.FilterExpressions.Add(x => x.Source == source);
            }

            if (term.IsNotNullOrWhiteSpace())
            {
                pagingSpec.FilterExpressions.Add(x =>
                    x.DisplayName.Contains(term) ||
                    x.NormalizedName.Contains(term) ||
                    x.Source.Contains(term));
            }

            return GetPaged(pagingSpec);
        }

        public List<ContributorEvidence> GetNarratorEvidenceForIdentityIndex(string term, string source)
        {
            var cleanTerm = term.IsNotNullOrWhiteSpace() ? term : null;
            var cleanSource = source.IsNotNullOrWhiteSpace() ? source : null;

            return Query(x => x.Role == "narrator" &&
                              x.DisplayName != null &&
                              x.DisplayName != string.Empty &&
                              (cleanSource == null || x.Source == cleanSource) &&
                              (cleanTerm == null ||
                               x.DisplayName.Contains(cleanTerm) ||
                               x.NormalizedName.Contains(cleanTerm) ||
                               x.Source.Contains(cleanTerm)))
                .OrderByDescending(x => x.Updated)
                .ToList();
        }

        public List<ContributorEvidence> GetNarratorEvidenceByNames(IEnumerable<string> normalizedNames, string source = null)
        {
            var names = normalizedNames.Where(x => x.IsNotNullOrWhiteSpace()).Distinct().ToList();
            var cleanSource = source.IsNotNullOrWhiteSpace() ? source : null;

            if (!names.Any())
            {
                return new List<ContributorEvidence>();
            }

            return Query(x => x.Role == "narrator" &&
                              x.NormalizedName != null &&
                              names.Contains(x.NormalizedName) &&
                              (cleanSource == null || x.Source == cleanSource))
                .OrderByDescending(x => x.Updated)
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

        public void DeleteByEditionIdsAndSources(IEnumerable<int> editionIds, IEnumerable<string> sources)
        {
            var ids = editionIds.Distinct().ToList();
            var sourceList = sources.Where(x => x.IsNotNullOrWhiteSpace()).Distinct().ToList();

            if (!ids.Any() || !sourceList.Any())
            {
                return;
            }

            Delete(x => x.EditionId.HasValue && ids.Contains(x.EditionId.Value) && sourceList.Contains(x.Source));
        }
    }
}
