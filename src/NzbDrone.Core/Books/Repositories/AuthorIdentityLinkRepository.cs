using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Books
{
    public interface IAuthorIdentityLinkRepository : IBasicRepository<AuthorIdentityLink>
    {
        AuthorIdentityLink FindByAuthorPair(int leftAuthorId, int rightAuthorId);
        List<AuthorIdentityLink> GetByAuthorId(int authorId);
        void DeleteByAuthorId(int authorId);
    }

    public class AuthorIdentityLinkRepository : BasicRepository<AuthorIdentityLink>, IAuthorIdentityLinkRepository
    {
        public AuthorIdentityLinkRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public AuthorIdentityLink FindByAuthorPair(int leftAuthorId, int rightAuthorId)
        {
            return Query(x => (x.CanonicalAuthorId == leftAuthorId && x.AliasAuthorId == rightAuthorId) ||
                              (x.CanonicalAuthorId == rightAuthorId && x.AliasAuthorId == leftAuthorId))
                .SingleOrDefault();
        }

        public List<AuthorIdentityLink> GetByAuthorId(int authorId)
        {
            return Query(x => x.CanonicalAuthorId == authorId || x.AliasAuthorId == authorId);
        }

        public void DeleteByAuthorId(int authorId)
        {
            Delete(x => x.CanonicalAuthorId == authorId || x.AliasAuthorId == authorId);
        }
    }
}
