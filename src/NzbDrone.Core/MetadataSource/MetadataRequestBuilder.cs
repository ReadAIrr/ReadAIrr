using NzbDrone.Common.Cloud;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.MetadataSource
{
    public interface IMetadataRequestBuilder
    {
        IHttpRequestBuilderFactory GetRequestBuilder();
    }

    public class MetadataRequestBuilder : IMetadataRequestBuilder
    {
        private readonly IConfigService _configService;

        private readonly IReadarrCloudRequestBuilder _defaultRequestFactory;

        public MetadataRequestBuilder(IConfigService configService, IReadarrCloudRequestBuilder defaultRequestBuilder)
        {
            _configService = configService;
            _defaultRequestFactory = defaultRequestBuilder;
        }

        public IHttpRequestBuilderFactory GetRequestBuilder()
        {
            var metadataSource = _configService.MetadataSource;

            if (metadataSource.IsNullOrWhiteSpace())
            {
                metadataSource = MetadataSourceConfig.LocalRReadingGlasses;
            }

            if (MetadataSourceConfig.IsOriginalReadarr(metadataSource))
            {
                return _defaultRequestFactory.Metadata;
            }

            return new HttpRequestBuilder(metadataSource.TrimEnd("/") + "/{route}").KeepAlive().CreateFactory();
        }
    }
}
