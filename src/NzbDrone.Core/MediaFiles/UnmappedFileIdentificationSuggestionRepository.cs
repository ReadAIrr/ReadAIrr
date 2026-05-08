using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaFiles
{
    public interface IUnmappedFileIdentificationSuggestionRepository : IBasicRepository<UnmappedFileIdentificationSuggestion>
    {
        List<UnmappedFileIdentificationSuggestion> GetByBookFileIds(IEnumerable<int> bookFileIds);
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
