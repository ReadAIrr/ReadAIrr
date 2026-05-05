using NzbDrone.Core.Configuration;
using Readarr.Http.REST;

namespace Prowlarr.Api.V1.Config
{
    public class DevelopmentConfigResource : RestResource
    {
        public string MetadataSource { get; set; }
        public int MinimumBookMatchSimilarity { get; set; }
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

    public static class DevelopmentConfigResourceMapper
    {
        public static DevelopmentConfigResource ToResource(this IConfigFileProvider model, IConfigService configService)
        {
            return new DevelopmentConfigResource
            {
                MetadataSource = configService.MetadataSource,
                MinimumBookMatchSimilarity = configService.MinimumBookMatchSimilarity,
                ConsoleLogLevel = model.ConsoleLogLevel,
                LogSql = model.LogSql,
                LogRotate = model.LogRotate,
                FilterSentryEvents = model.FilterSentryEvents
            };
        }
    }
}
