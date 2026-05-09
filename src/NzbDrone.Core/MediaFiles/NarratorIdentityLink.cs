using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.MediaFiles
{
    public class NarratorIdentityLink : ModelBase
    {
        public string CanonicalName { get; set; }
        public string CanonicalNormalizedName { get; set; }
        public string AliasName { get; set; }
        public string AliasNormalizedName { get; set; }
        public string RelationshipType { get; set; }
        public string DisplayPreference { get; set; }
    }
}
