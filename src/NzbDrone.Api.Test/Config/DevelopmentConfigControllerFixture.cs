using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Test.Common;
using Prowlarr.Api.V1.Config;
using Readarr.Api.V1.BookFiles;

namespace NzbDrone.Api.Test.Config
{
    [TestFixture]
    public class DevelopmentConfigControllerFixture : TestBase
    {
        private Mock<IConfigFileProvider> _configFileProvider;
        private Mock<IConfigService> _configService;
        private Mock<IMetadataSourceHealthService> _metadataSourceHealthService;
        private Mock<IUnmappedIdentificationSuggestionService> _unmappedIdentificationSuggestionService;
        private DevelopmentConfigController _subject;

        [SetUp]
        public void SetUp()
        {
            _configFileProvider = new Mock<IConfigFileProvider>();
            _configService = new Mock<IConfigService>();
            _metadataSourceHealthService = new Mock<IMetadataSourceHealthService>();
            _unmappedIdentificationSuggestionService = new Mock<IUnmappedIdentificationSuggestionService>();

            _subject = new DevelopmentConfigController(
                _configFileProvider.Object,
                _configService.Object,
                _metadataSourceHealthService.Object,
                _unmappedIdentificationSuggestionService.Object);
        }

        [Test]
        public void test_should_default_blank_metadata_source_to_hosted_goodreads()
        {
            _metadataSourceHealthService.Setup(x => x.Test(MetadataSourceConfig.GoodreadsHosted))
                .Returns(new MetadataSourceHealthResult
                {
                    MetadataSource = MetadataSourceConfig.GoodreadsHosted,
                    IsHealthy = true,
                    Message = "Metadata source is reachable",
                    Detail = "Author lookup completed in 24 ms",
                    StatusCode = 200,
                    ResponseTimeMs = 24
                });

            var result = _subject.TestDevelopmentConfig(new DevelopmentConfigTestResource());

            result.MetadataSource.Should().Be(MetadataSourceConfig.GoodreadsHosted);
            result.IsHealthy.Should().BeTrue();
            result.StatusCode.Should().Be(200);
            _metadataSourceHealthService.Verify(x => x.Test(MetadataSourceConfig.GoodreadsHosted), Times.Once);
        }

        [Test]
        public void test_should_reject_invalid_metadata_source_without_calling_health_service()
        {
            var result = _subject.TestDevelopmentConfig(new DevelopmentConfigTestResource
            {
                MetadataSource = "not a url"
            });

            result.MetadataSource.Should().Be("not a url");
            result.IsHealthy.Should().BeFalse();
            result.Message.Should().Contain("valid URL");
            _metadataSourceHealthService.Verify(x => x.Test(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void test_should_pass_custom_metadata_source_to_health_service()
        {
            const string metadataSource = "https://metadata.example.test/v1";

            _metadataSourceHealthService.Setup(x => x.Test(metadataSource))
                .Returns(new MetadataSourceHealthResult
                {
                    MetadataSource = metadataSource,
                    IsHealthy = false,
                    Message = "Metadata source returned HTTP 503",
                    Detail = "Service unavailable",
                    StatusCode = 503,
                    ResponseTimeMs = 120
                });

            var result = _subject.TestDevelopmentConfig(new DevelopmentConfigTestResource
            {
                MetadataSource = metadataSource
            });

            result.MetadataSource.Should().Be(metadataSource);
            result.IsHealthy.Should().BeFalse();
            result.Message.Should().Be("Metadata source returned HTTP 503");
            result.Detail.Should().Be("Service unavailable");
            result.StatusCode.Should().Be(503);
            result.ResponseTimeMs.Should().Be(120);
            _metadataSourceHealthService.Verify(x => x.Test(metadataSource), Times.Once);
        }
    }
}
