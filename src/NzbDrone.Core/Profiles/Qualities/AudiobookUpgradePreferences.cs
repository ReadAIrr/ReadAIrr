namespace NzbDrone.Core.Profiles.Qualities
{
    public enum AudiobookLayoutPreference
    {
        NoPreference = 0,
        PreferSingleFile = 1,
        PreferMultiFile = 2
    }

    public enum AudiobookFormatPreference
    {
        NoPreference = 0,
        PreferM4B = 1,
        PreferMP3 = 2
    }

    public enum AudiobookFileCountPreference
    {
        NoPreference = 0,
        PreferFewerParts = 1
    }

    public static class AudiobookShapePreference
    {
        public const string MultiFileMP3 = "multiFileMp3";
        public const string MultiFileM4B = "multiFileM4B";
        public const string SingleFileMP3 = "singleFileMp3";
        public const string SingleFileM4B = "singleFileM4B";
    }
}
