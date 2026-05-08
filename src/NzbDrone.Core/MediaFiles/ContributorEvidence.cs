using System;
using System.Linq;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.MediaFiles
{
    public class ContributorEvidence : ModelBase
    {
        public int? BookFileId { get; set; }
        public int? EditionId { get; set; }
        public string ForeignEditionId { get; set; }
        public string Role { get; set; }
        public string DisplayName { get; set; }
        public string NormalizedName { get; set; }
        public string Source { get; set; }
        public int? Confidence { get; set; }
        public string RawValue { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }

        public static string NormalizeName(string value)
        {
            return new string((value ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray());
        }
    }
}
