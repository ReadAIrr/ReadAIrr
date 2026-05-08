using NzbDrone.Core.Configuration;
using Readarr.Http.REST;

namespace Prowlarr.Api.V1.Config
{
    public class DevelopmentConfigResource : RestResource
    {
        public string MetadataSource { get; set; }
        public int MinimumBookMatchSimilarity { get; set; }
        public bool OpenRouterEnabled { get; set; }
        public string OpenRouterApiKey { get; set; }
        public string OpenRouterBaseUrl { get; set; }
        public string OpenRouterModel { get; set; }
        public int OpenRouterTimeout { get; set; }
        public int OpenRouterMaxFileContext { get; set; }
        public string ConsoleLogLevel { get; set; }
        public bool LogSql { get; set; }
        public int LogRotate { get; set; }
        public bool FilterSentryEvents { get; set; }
    }

    public class DevelopmentConfigTestResource
    {
        public string MetadataSource { get; set; }
        public bool IsHealthy { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }
        public int? StatusCode { get; set; }
        public double ResponseTimeMs { get; set; }
    }

    public class OpenRouterConfigTestResource
    {
        public bool OpenRouterEnabled { get; set; }
        public string OpenRouterApiKey { get; set; }
        public string OpenRouterBaseUrl { get; set; }
        public string OpenRouterModel { get; set; }
        public int OpenRouterTimeout { get; set; }
        public int OpenRouterMaxFileContext { get; set; }
        public bool IsHealthy { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }
        public int? StatusCode { get; set; }
        public double ResponseTimeMs { get; set; }
    }

    public static class DevelopmentConfigResourceMapper
    {
        public const string RedactedSecret = "********";

        public static DevelopmentConfigResource ToResource(this IConfigFileProvider model, IConfigService configService)
        {
            return new DevelopmentConfigResource
            {
                MetadataSource = configService.MetadataSource,
                MinimumBookMatchSimilarity = configService.MinimumBookMatchSimilarity,
                OpenRouterEnabled = configService.OpenRouterEnabled,
                OpenRouterApiKey = string.IsNullOrWhiteSpace(configService.OpenRouterApiKey) ? string.Empty : RedactedSecret,
                OpenRouterBaseUrl = configService.OpenRouterBaseUrl,
                OpenRouterModel = configService.OpenRouterModel,
                OpenRouterTimeout = configService.OpenRouterTimeout,
                OpenRouterMaxFileContext = configService.OpenRouterMaxFileContext,
                ConsoleLogLevel = model.ConsoleLogLevel,
                LogSql = model.LogSql,
                LogRotate = model.LogRotate,
                FilterSentryEvents = model.FilterSentryEvents
            };
        }
    }
}
