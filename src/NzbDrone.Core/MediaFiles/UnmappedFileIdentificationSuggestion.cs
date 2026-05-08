using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.MediaFiles
{
    public class UnmappedFileIdentificationSuggestion : ModelBase
    {
        public int BookFileId { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public DateTime Modified { get; set; }
        public string Type { get; set; }
        public string Provider { get; set; }
        public string Status { get; set; }
        public string LikelyAuthor { get; set; }
        public string LikelyBook { get; set; }
        public string LikelyEdition { get; set; }
        public string Language { get; set; }
        public string Narrator { get; set; }
        public int? Confidence { get; set; }
        public string Explanation { get; set; }
        public bool RequiresManualConfirmation { get; set; }
        public string TranscriptExcerpt { get; set; }
        public string ContextSummary { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
    }
}
