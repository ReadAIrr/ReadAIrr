using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NLog;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Http.Proxy;
using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Security;

namespace NzbDrone.Core.Configuration
{
    public enum ConfigKey
    {
        DownloadedBooksFolder
    }

    public class ConfigService : IConfigService
    {
        private readonly IConfigRepository _repository;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;
        private static Dictionary<string, string> _cache;

        public ConfigService(IConfigRepository repository, IEventAggregator eventAggregator, Logger logger)
        {
            _repository = repository;
            _eventAggregator = eventAggregator;
            _logger = logger;
            _cache = new Dictionary<string, string>();
        }

        private Dictionary<string, object> AllWithDefaults()
        {
            var dict = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);

            var type = GetType();
            var properties = type.GetProperties();

            foreach (var propertyInfo in properties)
            {
                var value = propertyInfo.GetValue(this, null);
                dict.Add(propertyInfo.Name, value);
            }

            return dict;
        }

        public void SaveConfigDictionary(Dictionary<string, object> configValues)
        {
            var allWithDefaults = AllWithDefaults();

            foreach (var configValue in configValues)
            {
                allWithDefaults.TryGetValue(configValue.Key, out var currentValue);
                if (currentValue == null || configValue.Value == null)
                {
                    continue;
                }

                var equal = configValue.Value.ToString().Equals(currentValue.ToString());

                if (!equal)
                {
                    SetValue(configValue.Key, configValue.Value.ToString());
                }
            }

            _eventAggregator.PublishEvent(new ConfigSavedEvent());
        }

        public bool IsDefined(string key)
        {
            return _repository.Get(key.ToLower()) != null;
        }

        public bool AutoUnmonitorPreviouslyDownloadedBooks
        {
            get { return GetValueBoolean("AutoUnmonitorPreviouslyDownloadedBooks"); }
            set { SetValue("AutoUnmonitorPreviouslyDownloadedBooks", value); }
        }

        public int Retention
        {
            get { return GetValueInt("Retention", 0); }
            set { SetValue("Retention", value); }
        }

        public string RecycleBin
        {
            get { return GetValue("RecycleBin", string.Empty); }
            set { SetValue("RecycleBin", value); }
        }

        public int RecycleBinCleanupDays
        {
            get { return GetValueInt("RecycleBinCleanupDays", 7); }
            set { SetValue("RecycleBinCleanupDays", value); }
        }

        public int RssSyncInterval
        {
            get { return GetValueInt("RssSyncInterval", 15); }

            set { SetValue("RssSyncInterval", value); }
        }

        public int MaximumSize
        {
            get { return GetValueInt("MaximumSize", 0); }

            set { SetValue("MaximumSize", value); }
        }

        public int MinimumAge
        {
            get { return GetValueInt("MinimumAge", 0); }

            set { SetValue("MinimumAge", value); }
        }

        public ProperDownloadTypes DownloadPropersAndRepacks
        {
            get { return GetValueEnum("DownloadPropersAndRepacks", ProperDownloadTypes.PreferAndUpgrade); }

            set { SetValue("DownloadPropersAndRepacks", value); }
        }

        public bool EnableCompletedDownloadHandling
        {
            get { return GetValueBoolean("EnableCompletedDownloadHandling", true); }

            set { SetValue("EnableCompletedDownloadHandling", value); }
        }

        public bool AutoRedownloadFailed
        {
            get { return GetValueBoolean("AutoRedownloadFailed", true); }

            set { SetValue("AutoRedownloadFailed", value); }
        }

        public bool AutoRedownloadFailedFromInteractiveSearch
        {
            get { return GetValueBoolean("AutoRedownloadFailedFromInteractiveSearch", true); }

            set { SetValue("AutoRedownloadFailedFromInteractiveSearch", value); }
        }

        public bool CreateEmptyAuthorFolders
        {
            get { return GetValueBoolean("CreateEmptyAuthorFolders", false); }

            set { SetValue("CreateEmptyAuthorFolders", value); }
        }

        public bool DeleteEmptyFolders
        {
            get { return GetValueBoolean("DeleteEmptyFolders", false); }

            set { SetValue("DeleteEmptyFolders", value); }
        }

        public FileDateType FileDate
        {
            get { return GetValueEnum("FileDate", FileDateType.None); }

            set { SetValue("FileDate", value); }
        }

        public string DownloadClientWorkingFolders
        {
            get { return GetValue("DownloadClientWorkingFolders", "_UNPACK_|_FAILED_"); }
            set { SetValue("DownloadClientWorkingFolders", value); }
        }

        public int DownloadClientHistoryLimit
        {
            get { return GetValueInt("DownloadClientHistoryLimit", 60); }

            set { SetValue("DownloadClientHistoryLimit", value); }
        }

        public bool SkipFreeSpaceCheckWhenImporting
        {
            get { return GetValueBoolean("SkipFreeSpaceCheckWhenImporting", false); }

            set { SetValue("SkipFreeSpaceCheckWhenImporting", value); }
        }

        public int MinimumFreeSpaceWhenImporting
        {
            get { return GetValueInt("MinimumFreeSpaceWhenImporting", 100); }

            set { SetValue("MinimumFreeSpaceWhenImporting", value); }
        }

        public bool CopyUsingHardlinks
        {
            get { return GetValueBoolean("CopyUsingHardlinks", true); }

            set { SetValue("CopyUsingHardlinks", value); }
        }

        public bool ImportExtraFiles
        {
            get { return GetValueBoolean("ImportExtraFiles", false); }

            set { SetValue("ImportExtraFiles", value); }
        }

        public string ExtraFileExtensions
        {
            get { return GetValue("ExtraFileExtensions", "srt"); }

            set { SetValue("ExtraFileExtensions", value); }
        }

        public bool WatchLibraryForChanges
        {
            get { return GetValueBoolean("WatchLibraryForChanges", true); }

            set { SetValue("WatchLibraryForChanges", value); }
        }

        public RescanAfterRefreshType RescanAfterRefresh
        {
            get { return GetValueEnum("RescanAfterRefresh", RescanAfterRefreshType.Always); }

            set { SetValue("RescanAfterRefresh", value); }
        }

        public AllowFingerprinting AllowFingerprinting
        {
            get { return GetValueEnum("AllowFingerprinting", AllowFingerprinting.NewFiles); }

            set { SetValue("AllowFingerprinting", value); }
        }

        public bool SetPermissionsLinux
        {
            get { return GetValueBoolean("SetPermissionsLinux", false); }

            set { SetValue("SetPermissionsLinux", value); }
        }

        public string ChmodFolder
        {
            get { return GetValue("ChmodFolder", "755"); }

            set { SetValue("ChmodFolder", value); }
        }

        public string ChownGroup
        {
            get { return GetValue("ChownGroup", ""); }

            set { SetValue("ChownGroup", value); }
        }

        public string MetadataSource
        {
            get { return GetValue("MetadataSource", MetadataSourceConfig.GoodreadsHosted); }

            set { SetValue("MetadataSource", value); }
        }

        public int MinimumBookMatchSimilarity
        {
            get { return GetValueInt("MinimumBookMatchSimilarity", 80); }

            set { SetValue("MinimumBookMatchSimilarity", value); }
        }

        public bool OpenRouterEnabled
        {
            get { return GetValueBoolean("OpenRouterEnabled", false); }

            set { SetValue("OpenRouterEnabled", value); }
        }

        public string OpenRouterApiKey
        {
            get { return GetValue("OpenRouterApiKey", string.Empty); }

            set { SetValue("OpenRouterApiKey", value); }
        }

        public string OpenRouterBaseUrl
        {
            get { return GetValue("OpenRouterBaseUrl", "https://openrouter.ai/api/v1"); }

            set { SetValue("OpenRouterBaseUrl", value); }
        }

        public string OpenRouterModel
        {
            get { return GetValue("OpenRouterModel", "openai/gpt-4.1-mini"); }

            set { SetValue("OpenRouterModel", value); }
        }

        public int OpenRouterTimeout
        {
            get { return GetValueInt("OpenRouterTimeout", 30); }

            set { SetValue("OpenRouterTimeout", value); }
        }

        public int OpenRouterMaxFileContext
        {
            get { return GetValueInt("OpenRouterMaxFileContext", 5); }

            set { SetValue("OpenRouterMaxFileContext", value); }
        }

        public string SpeechToTextProvider
        {
            get { return GetValue("SpeechToTextProvider", "disabled"); }

            set { SetValue("SpeechToTextProvider", value); }
        }

        public string SpeechToTextApiKey
        {
            get { return GetValue("SpeechToTextApiKey", string.Empty); }

            set { SetValue("SpeechToTextApiKey", value); }
        }

        public string SpeechToTextBaseUrl
        {
            get { return GetValue("SpeechToTextBaseUrl", string.Empty); }

            set { SetValue("SpeechToTextBaseUrl", value); }
        }

        public string SpeechToTextModel
        {
            get { return GetValue("SpeechToTextModel", string.Empty); }

            set { SetValue("SpeechToTextModel", value); }
        }

        public int SpeechToTextIntroSeconds
        {
            get { return GetValueInt("SpeechToTextIntroSeconds", 60); }

            set { SetValue("SpeechToTextIntroSeconds", value); }
        }

        public WriteAudioTagsType WriteAudioTags
        {
            get { return GetValueEnum("WriteAudioTags", WriteAudioTagsType.No); }

            set { SetValue("WriteAudioTags", value); }
        }

        public bool ScrubAudioTags
        {
            get { return GetValueBoolean("ScrubAudioTags", false); }

            set { SetValue("ScrubAudioTags", value); }
        }

        public WriteBookTagsType WriteBookTags
        {
            get { return GetValueEnum("WriteBookTags", WriteBookTagsType.NewFiles); }

            set { SetValue("WriteBookTags", value); }
        }

        public bool UpdateCovers
        {
            get { return GetValueBoolean("UpdateCovers", true); }

            set { SetValue("UpdateCovers", value); }
        }

        public bool EmbedMetadata
        {
            get { return GetValueBoolean("EmbedMetadata", false); }

            set { SetValue("EmbedMetadata", value); }
        }

        public int FirstDayOfWeek
        {
            get { return GetValueInt("FirstDayOfWeek", (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek); }

            set { SetValue("FirstDayOfWeek", value); }
        }

        public string CalendarWeekColumnHeader
        {
            get { return GetValue("CalendarWeekColumnHeader", "ddd M/D"); }

            set { SetValue("CalendarWeekColumnHeader", value); }
        }

        public string ShortDateFormat
        {
            get { return GetValue("ShortDateFormat", "MMM D YYYY"); }

            set { SetValue("ShortDateFormat", value); }
        }

        public string LongDateFormat
        {
            get { return GetValue("LongDateFormat", "dddd, MMMM D YYYY"); }

            set { SetValue("LongDateFormat", value); }
        }

        public string TimeFormat
        {
            get { return GetValue("TimeFormat", "h(:mm)a"); }

            set { SetValue("TimeFormat", value); }
        }

        public bool ShowRelativeDates
        {
            get { return GetValueBoolean("ShowRelativeDates", true); }

            set { SetValue("ShowRelativeDates", value); }
        }

        public bool EnableColorImpairedMode
        {
            get { return GetValueBoolean("EnableColorImpairedMode", false); }

            set { SetValue("EnableColorImpairedMode", value); }
        }

        public int UILanguage
        {
            get { return GetValueInt("UILanguage", (int)Language.English); }

            set { SetValue("UILanguage", value); }
        }

        public bool CleanupMetadataImages
        {
            get { return GetValueBoolean("CleanupMetadataImages", true); }

            set { SetValue("CleanupMetadataImages", value); }
        }

        public string PlexClientIdentifier => GetValue("PlexClientIdentifier", Guid.NewGuid().ToString(), true);

        public string RijndaelPassphrase => GetValue("RijndaelPassphrase", Guid.NewGuid().ToString(), true);

        public string HmacPassphrase => GetValue("HmacPassphrase", Guid.NewGuid().ToString(), true);

        public string RijndaelSalt => GetValue("RijndaelSalt", Guid.NewGuid().ToString(), true);

        public string HmacSalt => GetValue("HmacSalt", Guid.NewGuid().ToString(), true);

        public bool ProxyEnabled => GetValueBoolean("ProxyEnabled", false);

        public ProxyType ProxyType => GetValueEnum<ProxyType>("ProxyType", ProxyType.Http);

        public string ProxyHostname => GetValue("ProxyHostname", string.Empty);

        public int ProxyPort => GetValueInt("ProxyPort", 8080);

        public string ProxyUsername => GetValue("ProxyUsername", string.Empty);

        public string ProxyPassword => GetValue("ProxyPassword", string.Empty);

        public string ProxyBypassFilter => GetValue("ProxyBypassFilter", string.Empty);

        public bool ProxyBypassLocalAddresses => GetValueBoolean("ProxyBypassLocalAddresses", true);

        public string BackupFolder => GetValue("BackupFolder", "Backups");

        public int BackupInterval => GetValueInt("BackupInterval", 7);

        public int BackupRetention => GetValueInt("BackupRetention", 28);

        public CertificateValidationType CertificateValidation =>
            GetValueEnum("CertificateValidation", CertificateValidationType.Enabled);

        public string ApplicationUrl => GetValue("ApplicationUrl", string.Empty);

        public bool TrustCgnatIpAddresses
        {
            get { return GetValueBoolean("TrustCgnatIpAddresses", false); }
            set { SetValue("TrustCgnatIpAddresses", value); }
        }

        private string GetValue(string key)
        {
            return GetValue(key, string.Empty);
        }

        private bool GetValueBoolean(string key, bool defaultValue = false)
        {
            return Convert.ToBoolean(GetValue(key, defaultValue));
        }

        private int GetValueInt(string key, int defaultValue = 0)
        {
            return Convert.ToInt32(GetValue(key, defaultValue));
        }

        private T GetValueEnum<T>(string key, T defaultValue)
        {
            return (T)Enum.Parse(typeof(T), GetValue(key, defaultValue), true);
        }

        public string GetValue(string key, object defaultValue, bool persist = false)
        {
            key = key.ToLowerInvariant();
            Ensure.That(key, () => key).IsNotNullOrWhiteSpace();

            EnsureCache();

            if (_cache.TryGetValue(key, out var dbValue) && dbValue != null && !string.IsNullOrEmpty(dbValue))
            {
                return dbValue;
            }

            _logger.Trace("Using default config value for '{0}' defaultValue:'{1}'", key, defaultValue);

            if (persist)
            {
                SetValue(key, defaultValue.ToString());
            }

            return defaultValue.ToString();
        }

        private void SetValue(string key, bool value)
        {
            SetValue(key, value.ToString());
        }

        private void SetValue(string key, int value)
        {
            SetValue(key, value.ToString());
        }

        private void SetValue(string key, Enum value)
        {
            SetValue(key, value.ToString().ToLower());
        }

        private void SetValue(string key, string value)
        {
            key = key.ToLowerInvariant();

            var logValue = IsSensitiveKey(key) ? "<redacted>" : value;
            _logger.Trace("Writing Setting to database. Key:'{0}' Value:'{1}'", key, logValue);
            _repository.Upsert(key, value);

            ClearCache();
        }

        private static bool IsSensitiveKey(string key)
        {
            return key.Contains("apikey") ||
                   key.Contains("password") ||
                   key.Contains("secret") ||
                   key.Contains("token");
        }

        private void EnsureCache()
        {
            lock (_cache)
            {
                if (!_cache.Any())
                {
                    var all = _repository.All();
                    _cache = all.ToDictionary(c => c.Key.ToLower(), c => c.Value);
                }
            }
        }

        private static void ClearCache()
        {
            lock (_cache)
            {
                _cache = new Dictionary<string, string>();
            }
        }
    }
}
