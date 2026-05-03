using System;

namespace NzbDrone.Core.MetadataSource
{
    public static class MetadataSourceConfig
    {
        public const string GoodreadsHosted = "https://api.bookinfo.pro";
        public const string HardcoverHosted = "https://hardcover.bookinfo.pro";
        public const string LocalRReadingGlasses = "http://rreading-glasses:8788";
        public const string OriginalReadarr = "readarr://metadata/original";

        public static bool IsOriginalReadarr(string metadataSource)
        {
            return string.Equals(metadataSource, OriginalReadarr, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
