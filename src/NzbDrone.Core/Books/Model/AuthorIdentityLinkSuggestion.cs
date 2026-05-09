using System.Collections.Generic;

namespace NzbDrone.Core.Books
{
    public class AuthorIdentityLinkSuggestion
    {
        public int AuthorId { get; set; }
        public string AuthorName { get; set; }
        public string TitleSlug { get; set; }
        public string RelationshipType { get; set; }
        public string DisplayPreference { get; set; }
        public int Confidence { get; set; }
        public string ConfidenceLabel { get; set; }
        public List<AuthorIdentityLinkSuggestionReason> Reasons { get; set; } = new List<AuthorIdentityLinkSuggestionReason>();
    }

    public class AuthorIdentityLinkSuggestionReason
    {
        public string Kind { get; set; }
        public string Label { get; set; }
        public string Detail { get; set; }
    }
}
