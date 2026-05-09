using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaFiles
{
    public interface INarratorIdentityLinkRepository : IBasicRepository<NarratorIdentityLink>
    {
        NarratorIdentityLink FindByAlias(string aliasNormalizedName);
        List<NarratorIdentityLink> GetByNormalizedName(string normalizedName);
    }

    public class NarratorIdentityLinkRepository : BasicRepository<NarratorIdentityLink>, INarratorIdentityLinkRepository
    {
        public NarratorIdentityLinkRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public NarratorIdentityLink FindByAlias(string aliasNormalizedName)
        {
            return Query(x => x.AliasNormalizedName == aliasNormalizedName).SingleOrDefault();
        }

        public List<NarratorIdentityLink> GetByNormalizedName(string normalizedName)
        {
            return Query(x => x.CanonicalNormalizedName == normalizedName || x.AliasNormalizedName == normalizedName);
        }
    }
}
