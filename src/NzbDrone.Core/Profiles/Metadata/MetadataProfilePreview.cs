using System.Collections.Generic;

namespace NzbDrone.Core.Profiles.Metadata
{
    public class MetadataProfilePreview
    {
        public MetadataProfilePreview()
        {
            Rules = new List<MetadataProfilePreviewRule>();
        }

        public int EvaluatedBooks { get; set; }
        public int EvaluatedEditions { get; set; }
        public List<MetadataProfilePreviewRule> Rules { get; set; }
    }

    public class MetadataProfilePreviewRule
    {
        public MetadataProfilePreviewRule()
        {
            Examples = new List<MetadataProfilePreviewExample>();
        }

        public string Key { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public int Count { get; set; }
        public List<MetadataProfilePreviewExample> Examples { get; set; }
    }

    public class MetadataProfilePreviewExample
    {
        public string Title { get; set; }
        public string Detail { get; set; }
    }
}
