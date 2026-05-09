using System;
using System.Diagnostics;
using System.Text.Json;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.MetadataSource
{
    public interface IMetadataSourceHealthService
    {
        MetadataSourceHealthResult Test(string metadataSource);
    }

    public class MetadataSourceHealthResult
    {
        public bool IsHealthy { get; set; }
        public string MetadataSource { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }
        public int? StatusCode { get; set; }
        public double ResponseTimeMs { get; set; }
        public string ServiceVersion { get; set; }
        public string ServiceVersionDetail { get; set; }
    }

    public class MetadataSourceHealthService : IMetadataSourceHealthService
    {
        private const string TestAuthorId = "3389";

        private readonly IMetadataRequestBuilder _requestBuilder;
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public MetadataSourceHealthService(IMetadataRequestBuilder requestBuilder, IHttpClient httpClient, Logger logger)
        {
            _requestBuilder = requestBuilder;
            _httpClient = httpClient;
            _logger = logger;
        }

        public MetadataSourceHealthResult Test(string metadataSource)
        {
            var result = new MetadataSourceHealthResult
            {
                MetadataSource = metadataSource,
                Message = "Metadata source is reachable"
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var request = _requestBuilder.GetRequestBuilder(metadataSource).Create()
                    .SetSegment("route", $"author/{TestAuthorId}")
                    .Build();

                request.SuppressHttpError = true;

                var response = _httpClient.Get(request);

                stopwatch.Stop();
                result.ResponseTimeMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 0);
                result.StatusCode = (int)response.StatusCode;

                if (response.HasHttpError)
                {
                    result.IsHealthy = false;
                    result.Message = $"Metadata source returned HTTP {(int)response.StatusCode}";
                    result.Detail = Truncate(response.Content);

                    return result;
                }

                if (response.Content.IsNullOrWhiteSpace())
                {
                    result.IsHealthy = false;
                    result.Message = "Metadata source returned an empty response";

                    return result;
                }

                result.IsHealthy = true;
                result.Detail = $"Author lookup completed in {result.ResponseTimeMs} ms";
                (result.ServiceVersion, result.ServiceVersionDetail) = TryGetServiceVersion(metadataSource);

                return result;
            }
            catch (HttpException ex)
            {
                stopwatch.Stop();

                result.IsHealthy = false;
                result.ResponseTimeMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 0);
                result.StatusCode = ex.Response == null ? null : (int)ex.Response.StatusCode;
                result.Message = "Unable to communicate with the metadata source";
                result.Detail = ex.Message;

                _logger.Warn(ex, "Metadata source test failed for {metadataSource}", metadataSource);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                result.IsHealthy = false;
                result.ResponseTimeMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 0);
                result.Message = "Metadata source test failed";
                result.Detail = ex.Message;

                _logger.Warn(ex, "Metadata source test failed for {metadataSource}", metadataSource);

                return result;
            }
        }

        private static string Truncate(string value)
        {
            if (value.IsNullOrWhiteSpace() || value.Length <= 500)
            {
                return value;
            }

            return value.Substring(0, 500);
        }

        private (string Version, string Detail) TryGetServiceVersion(string metadataSource)
        {
            if (MetadataSourceConfig.IsOriginalReadarr(metadataSource))
            {
                return (null, "Version metadata does not apply to original Readarr compatibility metadata.");
            }

            try
            {
                var request = _requestBuilder.GetRequestBuilder(metadataSource).Create()
                    .SetSegment("route", "version")
                    .Build();

                request.SuppressHttpError = true;

                var response = _httpClient.Get(request);

                if (response.HasHttpError)
                {
                    return (null, $"Version endpoint returned HTTP {(int)response.StatusCode}.");
                }

                var version = ParseVersion(response.Content);

                return version.IsNotNullOrWhiteSpace() ?
                    (version, "Version endpoint returned metadata.") :
                    (null, "Version endpoint did not include a recognized version field.");
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Metadata source version probe failed for {metadataSource}", metadataSource);
                return (null, $"Version probe failed: {ex.GetType().Name}.");
            }
        }

        private static string ParseVersion(string content)
        {
            if (content.IsNullOrWhiteSpace())
            {
                return null;
            }

            var trimmed = content.Trim();

            if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
            {
                return Truncate(trimmed.Trim('"'));
            }

            try
            {
                using (var document = JsonDocument.Parse(trimmed))
                {
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        return null;
                    }

                    foreach (var propertyName in new[] { "version", "currentVersion", "buildVersion", "appVersion" })
                    {
                        if (document.RootElement.TryGetProperty(propertyName, out var property) &&
                            property.ValueKind == JsonValueKind.String)
                        {
                            return Truncate(property.GetString());
                        }
                    }
                }
            }
            catch (JsonException)
            {
                return null;
            }

            return null;
        }
    }
}
