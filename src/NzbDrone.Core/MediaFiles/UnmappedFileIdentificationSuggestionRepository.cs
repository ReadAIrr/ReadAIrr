using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaFiles
{
    public interface IUnmappedFileIdentificationSuggestionRepository : IBasicRepository<UnmappedFileIdentificationSuggestion>
    {
        List<UnmappedFileIdentificationSuggestion> GetByBookFileIds(IEnumerable<int> bookFileIds);
        List<int> GetBookFileIdsMatchingTerm(string term);
        List<UnmappedFileIdentificationSuggestion> GetRecent(int take);
        void DeleteByBookFileIds(IEnumerable<int> bookFileIds);
    }

    public class UnmappedFileIdentificationSuggestionRepository : BasicRepository<UnmappedFileIdentificationSuggestion>, IUnmappedFileIdentificationSuggestionRepository
    {
        public UnmappedFileIdentificationSuggestionRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<UnmappedFileIdentificationSuggestion> GetByBookFileIds(IEnumerable<int> bookFileIds)
        {
            var ids = bookFileIds.Distinct().ToList();

            if (!ids.Any())
            {
                return new List<UnmappedFileIdentificationSuggestion>();
            }

            return Query(x => ids.Contains(x.BookFileId))
                .OrderByDescending(x => x.Updated)
                .ToList();
        }

        public List<int> GetBookFileIdsMatchingTerm(string term)
        {
            term = term?.Trim();

            if (string.IsNullOrWhiteSpace(term))
            {
                return new List<int>();
            }

            return Query(x =>
                    x.Path.Contains(term) ||
                    x.Status.Contains(term) ||
                    x.LikelyAuthor.Contains(term) ||
                    x.LikelyBook.Contains(term) ||
                    x.LikelyEdition.Contains(term) ||
                    x.Language.Contains(term) ||
                    x.Narrator.Contains(term) ||
                    x.Explanation.Contains(term) ||
                    x.ContextSummary.Contains(term) ||
                    x.Stage.Contains(term))
                .Select(x => x.BookFileId)
                .Distinct()
                .ToList();
        }

        public List<UnmappedFileIdentificationSuggestion> GetRecent(int take)
        {
            if (take <= 0)
            {
                return new List<UnmappedFileIdentificationSuggestion>();
            }

            var spec = new PagingSpec<UnmappedFileIdentificationSuggestion>
            {
                Page = 1,
                PageSize = take,
                SortKey = "updated",
                SortDirection = SortDirection.Descending
            };

            return GetPaged(spec).Records;
        }

        public void DeleteByBookFileIds(IEnumerable<int> bookFileIds)
        {
            var ids = bookFileIds.Distinct().ToList();

            if (!ids.Any())
            {
                return;
            }

            Delete(x => ids.Contains(x.BookFileId));
        }
    }
}
