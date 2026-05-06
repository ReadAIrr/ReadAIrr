using System.Collections.Generic;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Profiles.Metadata
{
    public class MetadataProfile : ModelBase
    {
        public string Name { get; set; }
        public double MinPopularity { get; set; }
        public bool SkipMissingDate { get; set; }
        public bool SkipMissingIsbn { get; set; }
        public bool SkipPartsAndSets { get; set; }
        public bool SkipSeriesSecondary { get; set; }
        public bool RequireReadable { get; set; }
        public bool RequireAudio { get; set; }
        public bool SkipAnthologies { get; set; }
        public bool SkipCollections { get; set; }
        public bool SkipSerializedParts { get; set; }
        public bool SkipEssays { get; set; }
        public bool SkipShortStories { get; set; }
        public string AllowedLanguages { get; set; }
        public int MinPages { get; set; }
        public List<string> Ignored { get; set; }

        public MetadataProfile()
        {
            Ignored = new List<string>();
        }
    }
}
