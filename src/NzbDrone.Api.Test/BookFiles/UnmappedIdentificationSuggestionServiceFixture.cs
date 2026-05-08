using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Test.Common;
using Prowlarr.Api.V1.Config;
using Readarr.Api.V1.BookFiles;

namespace NzbDrone.Api.Test.BookFiles
{
    [TestFixture]
    public class UnmappedIdentificationSuggestionServiceFixture : TestBase
    {
        private Mock<IConfigService> _configService;
        private Mock<IHttpClient> _httpClient;
        private UnmappedIdentificationSuggestionService _subject;

        [SetUp]
        public void SetUp()
        {
            _configService = new Mock<IConfigService>();
            _httpClient = new Mock<IHttpClient>();

            _configService.SetupGet(x => x.OpenRouterBaseUrl).Returns("https://openrouter.ai/api/v1");
            _configService.SetupGet(x => x.OpenRouterModel).Returns("openai/gpt-4.1-mini");
            _configService.SetupGet(x => x.OpenRouterTimeout).Returns(30);
            _configService.SetupGet(x => x.OpenRouterMaxFileContext).Returns(5);

            _subject = new UnmappedIdentificationSuggestionService(_configService.Object, _httpClient.Object, TestLogger);
        }

        [Test]
        public void test_should_not_call_provider_when_openrouter_is_disabled()
        {
            var result = _subject.Test(new OpenRouterConfigTestResource
            {
                OpenRouterEnabled = false,
                OpenRouterBaseUrl = "https://openrouter.ai/api/v1",
                OpenRouterModel = "openai/gpt-4.1-mini",
                OpenRouterTimeout = 30,
                OpenRouterMaxFileContext = 5
            });

            result.IsHealthy.Should().BeFalse();
            result.Message.Should().Contain("disabled");
            _httpClient.Verify(x => x.Post(It.IsAny<HttpRequest>()), Times.Never);
        }

        [Test]
        public void ai_review_should_return_disabled_suggestions_without_api_key()
        {
            _configService.SetupGet(x => x.OpenRouterEnabled).Returns(true);
            _configService.SetupGet(x => x.OpenRouterApiKey).Returns(string.Empty);

            var result = _subject.ReviewWithAi(new List<BookFileResource>
            {
                new BookFileResource { Id = 1, Path = "/books/Alice Writer - Hidden.m4b" }
            });

            result.Should().ContainSingle();
            result[0].Status.Should().Be("disabled");
            result[0].Type.Should().Be("aiReview");
            _httpClient.Verify(x => x.Post(It.IsAny<HttpRequest>()), Times.Never);
        }

        [Test]
        public void deep_identify_should_only_mark_audio_files_provider_ready_when_configured()
        {
            _configService.SetupGet(x => x.OpenRouterEnabled).Returns(true);
            _configService.SetupGet(x => x.OpenRouterApiKey).Returns("test-key");

            var result = _subject.DeepIdentifyAudio(new List<BookFileResource>
            {
                new BookFileResource { Id = 1, Path = "/books/Alice Writer - Hidden.m4b" },
                new BookFileResource { Id = 2, Path = "/books/Alice Writer - Cover.jpg" }
            });

            result.Should().HaveCount(2);
            result[0].Status.Should().Be("providerReady");
            result[0].RequiresManualConfirmation.Should().BeTrue();
            result[1].Status.Should().Be("disabled");
            _httpClient.Verify(x => x.Post(It.IsAny<HttpRequest>()), Times.Never);
        }
    }
}
