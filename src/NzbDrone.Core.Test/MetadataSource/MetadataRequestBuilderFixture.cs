using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Cloud;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource
{
    [TestFixture]
    public class MetadataRequestBuilderFixture : CoreTest<MetadataRequestBuilder>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.MetadataSource)
                .Returns(MetadataSourceConfig.GoodreadsHosted);

            Mocker.GetMock<IReadarrCloudRequestBuilder>()
                .Setup(s => s.Metadata)
                .Returns(new HttpRequestBuilder("https://api.bookinfo.pro/v1/{route}").CreateFactory());
        }

        private void WithCustomProvider()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.MetadataSource)
                .Returns("http://api.readarr.com/api/testing/");
        }

        private void WithOriginalReadarrProvider()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.MetadataSource)
                .Returns(MetadataSourceConfig.OriginalReadarr);
        }

        private void WithBlankProvider()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.MetadataSource)
                .Returns("");
        }

        [TestCase]
        public void should_use_user_definied_if_not_blank()
        {
            WithCustomProvider();

            var details = Subject.GetRequestBuilder().Create();

            details.BaseUrl.ToString().Should().Contain("testing");
        }

        [TestCase]
        public void should_use_hosted_goodreads_by_default()
        {
            var details = Subject.GetRequestBuilder().Create();

            details.BaseUrl.ToString().Should().Contain("api.bookinfo.pro");
        }

        [TestCase]
        public void should_use_hosted_goodreads_if_config_blank()
        {
            WithBlankProvider();

            var details = Subject.GetRequestBuilder().Create();

            details.BaseUrl.ToString().Should().Contain("api.bookinfo.pro");
        }

        [TestCase]
        public void should_use_original_readarr_if_selected()
        {
            WithOriginalReadarrProvider();

            var details = Subject.GetRequestBuilder().Create();

            details.BaseUrl.ToString().Should().Contain("api.bookinfo.pro/v1");
        }
    }
}
