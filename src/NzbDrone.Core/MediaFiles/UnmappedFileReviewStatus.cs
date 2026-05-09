using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.MediaFiles
{
    public class UnmappedFileReviewStatus : ModelBase
    {
        public int BookFileId { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public DateTime Modified { get; set; }
        public string Status { get; set; }
        public string ReasonKinds { get; set; }
        public int? Confidence { get; set; }
        public string Source { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
    }
}
