using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Books
{
    public class AuthorIdentityLink : ModelBase
    {
        public int CanonicalAuthorId { get; set; }
        public int AliasAuthorId { get; set; }
        public string RelationshipType { get; set; }
        public string DisplayPreference { get; set; }
    }
}
