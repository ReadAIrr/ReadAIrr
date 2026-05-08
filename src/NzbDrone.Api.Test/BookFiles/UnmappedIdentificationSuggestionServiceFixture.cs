using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
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
        private IAudioIntroTranscriptionService _audioIntroTranscriptionService;
        private Mock<IUnmappedFileIdentificationSuggestionRepository> _suggestionRepository;
        private UnmappedIdentificationSuggestionService _subject;

        [SetUp]
        public void SetUp()
        {
            _configService = new Mock<IConfigService>();
            _httpClient = new Mock<IHttpClient>();
            _suggestionRepository = new Mock<IUnmappedFileIdentificationSuggestionRepository>();

            _configService.SetupGet(x => x.OpenRouterBaseUrl).Returns("https://openrouter.ai/api/v1");
            _configService.SetupGet(x => x.OpenRouterModel).Returns("openai/gpt-4.1-mini");
            _configService.SetupGet(x => x.OpenRouterTimeout).Returns(30);
            _configService.SetupGet(x => x.OpenRouterMaxFileContext).Returns(5);
            _configService.SetupGet(x => x.SpeechToTextProvider).Returns("disabled");
            _configService.SetupGet(x => x.SpeechToTextApiKey).Returns(string.Empty);
            _configService.SetupGet(x => x.SpeechToTextIntroSeconds).Returns(30);

            _audioIntroTranscriptionService = new AudioIntroTranscriptionService(_configService.Object);
            _subject = new UnmappedIdentificationSuggestionService(_configService.Object, _httpClient.Object, _audioIntroTranscriptionService, _suggestionRepository.Object, TestLogger);
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
            _suggestionRepository.Verify(x => x.Insert(It.IsAny<UnmappedFileIdentificationSuggestion>()), Times.Never);
        }

        [Test]
        public void deep_identify_should_return_disabled_when_stt_provider_is_disabled()
        {
            var result = _subject.DeepIdentifyAudio(new List<BookFileResource>
            {
                new BookFileResource { Id = 1, Path = "/books/Alice Writer - Hidden.m4b" },
                new BookFileResource { Id = 2, Path = "/books/Alice Writer - Cover.jpg" }
            });

            result.Should().HaveCount(2);
            result[0].Status.Should().Be("disabled");
            result[0].RequiresManualConfirmation.Should().BeTrue();
            result[1].Status.Should().Be("disabled");
            _httpClient.Verify(x => x.Post(It.IsAny<HttpRequest>()), Times.Never);
            _suggestionRepository.Verify(x => x.Insert(It.IsAny<UnmappedFileIdentificationSuggestion>()), Times.Never);
        }

        [Test]
        public void deep_identify_should_mark_audio_provider_ready_when_stt_provider_is_configured()
        {
            _configService.SetupGet(x => x.SpeechToTextProvider).Returns("openai-compatible");
            _configService.SetupGet(x => x.SpeechToTextApiKey).Returns("test-key");

            var result = _subject.DeepIdentifyAudio(new List<BookFileResource>
            {
                new BookFileResource { Id = 1, Path = "/books/Alice Writer - Hidden.m4b" },
                new BookFileResource { Id = 2, Path = "/books/Alice Writer - Cover.jpg" }
            });

            result.Should().HaveCount(2);
            result[0].Status.Should().Be("providerReady");
            result[0].Provider.Should().Be("openai-compatible");
            result[0].RequiresManualConfirmation.Should().BeTrue();
            result[1].Status.Should().Be("disabled");
            _httpClient.Verify(x => x.Post(It.IsAny<HttpRequest>()), Times.Never);
            _suggestionRepository.Verify(x => x.Insert(It.Is<UnmappedFileIdentificationSuggestion>(s => s.BookFileId == 1 && s.Status == "providerReady" && s.Provider == "openai-compatible")), Times.Once);
            _suggestionRepository.Verify(x => x.Insert(It.Is<UnmappedFileIdentificationSuggestion>(s => s.BookFileId == 2)), Times.Never);
        }

        [Test]
        public void audio_intro_transcript_clues_should_parse_spoken_book_evidence()
        {
            var result = _audioIntroTranscriptionService.ExtractClues("You're listening to The Hidden Book, written by Alice Writer, narrated by Jane Reader, published by Example Audio. Book 2 of The Hidden Series.");

            result.Title.Should().Be("The Hidden Book");
            result.Author.Should().Be("Alice Writer");
            result.Narrator.Should().Be("Jane Reader");
            result.Publisher.Should().Be("Example Audio");
            result.Series.Should().Be("The Hidden Series");
            result.Confidence.Should().Be(100);
        }

        [Test]
        public void persisted_suggestions_should_be_marked_stale_when_file_identity_changes()
        {
            var updated = new global::System.DateTime(2026, 05, 08, 20, 0, 0, global::System.DateTimeKind.Utc);

            _suggestionRepository.Setup(x => x.GetByBookFileIds(It.IsAny<IEnumerable<int>>()))
                .Returns(new List<UnmappedFileIdentificationSuggestion>
                {
                    new UnmappedFileIdentificationSuggestion
                    {
                        BookFileId = 1,
                        Path = "/books/Alice Writer - Hidden.m4b",
                        Size = 100,
                        Modified = updated.AddMinutes(-5),
                        Type = "aiReview",
                        Provider = "openrouter",
                        Status = "suggested",
                        LikelyAuthor = "Alice Writer",
                        LikelyBook = "Hidden",
                        RequiresManualConfirmation = true,
                        Created = updated.AddMinutes(-10),
                        Updated = updated.AddMinutes(-10)
                    }
                });

            var result = _subject.GetPersisted(new List<BookFileResource>
            {
                new BookFileResource
                {
                    Id = 1,
                    Path = "/books/Alice Writer - Hidden.m4b",
                    Size = 100,
                    Modified = updated
                }
            });

            result.Should().ContainSingle();
            result[0].IsStale.Should().BeTrue();
            result[0].LikelyAuthor.Should().Be("Alice Writer");
        }
    }
}
