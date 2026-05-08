using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Test.Common;
using Prowlarr.Api.V1.Config;
using Readarr.Api.V1.BookFiles;
using Readarr.Api.V1.ManualImport;

namespace NzbDrone.Api.Test.BookFiles
{
    [TestFixture]
    public class UnmappedIdentificationSuggestionServiceFixture : TestBase
    {
        private Mock<IConfigService> _configService;
        private Mock<IHttpClient> _httpClient;
        private Mock<IAudioIntroSegmentExtractor> _audioIntroSegmentExtractor;
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
            _configService.SetupGet(x => x.SpeechToTextBaseUrl).Returns("https://api.openai.com/v1");
            _configService.SetupGet(x => x.SpeechToTextModel).Returns("whisper-1");
            _configService.SetupGet(x => x.SpeechToTextIntroSeconds).Returns(30);

            _audioIntroSegmentExtractor = new Mock<IAudioIntroSegmentExtractor>();
            _audioIntroTranscriptionService = new AudioIntroTranscriptionService(_configService.Object, _httpClient.Object, _audioIntroSegmentExtractor.Object);
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
        public void queue_deep_identify_should_persist_queued_audio_and_skipped_non_audio()
        {
            var result = _subject.QueueDeepIdentifyAudio(new List<BookFileResource>
            {
                new BookFileResource { Id = 1, Path = "/books/Alice Writer - Hidden.m4b", Size = 100 },
                new BookFileResource { Id = 2, Path = "/books/Alice Writer - Cover.jpg", Size = 200 }
            });

            result.Should().HaveCount(2);
            result[0].Status.Should().Be("queued");
            result[1].Status.Should().Be("skipped");
            _suggestionRepository.Verify(x => x.DeleteByBookFileIds(It.Is<IEnumerable<int>>(ids => ids.Contains(1) && ids.Contains(2))), Times.Once);
            _suggestionRepository.Verify(x => x.Insert(It.Is<UnmappedFileIdentificationSuggestion>(s => s.BookFileId == 1 && s.Type == "deepAudio" && s.Status == "queued")), Times.Once);
            _suggestionRepository.Verify(x => x.Insert(It.Is<UnmappedFileIdentificationSuggestion>(s => s.BookFileId == 2 && s.Type == "deepAudio" && s.Status == "skipped")), Times.Once);
        }

        [Test]
        public void queued_deep_identify_should_persist_running_and_disabled_provider_state()
        {
            _configService.SetupGet(x => x.SpeechToTextProvider).Returns("openrouter");
            _configService.SetupGet(x => x.OpenRouterEnabled).Returns(true);
            _configService.SetupGet(x => x.OpenRouterApiKey).Returns(string.Empty);

            var result = _subject.ProcessQueuedDeepIdentifyAudio(new List<BookFileResource>
            {
                new BookFileResource { Id = 1, Path = "/books/Alice Writer - Hidden.m4b", Size = 100 }
            });

            result.Should().ContainSingle();
            result[0].Status.Should().Be("disabled");
            result[0].Provider.Should().Be("openrouter-stt");
            _audioIntroSegmentExtractor.Verify(x => x.Extract(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
            _suggestionRepository.Verify(x => x.Insert(It.Is<UnmappedFileIdentificationSuggestion>(s => s.BookFileId == 1 && s.Status == "extractingIntro" && s.Stage == "extractingIntro")), Times.Once);
            _suggestionRepository.Verify(x => x.Insert(It.Is<UnmappedFileIdentificationSuggestion>(s => s.BookFileId == 1 && s.Status == "disabled" && s.Provider == "openrouter-stt")), Times.Once);
        }

        [Test]
        public void deep_identify_should_return_disabled_when_openrouter_stt_is_selected_without_openrouter_key()
        {
            _configService.SetupGet(x => x.SpeechToTextProvider).Returns("openrouter");
            _configService.SetupGet(x => x.OpenRouterEnabled).Returns(true);
            _configService.SetupGet(x => x.OpenRouterApiKey).Returns(string.Empty);

            var result = _subject.DeepIdentifyAudio(new List<BookFileResource>
            {
                new BookFileResource { Id = 1, Path = "/books/Alice Writer - Hidden.m4b" }
            });

            result.Should().ContainSingle();
            result[0].Status.Should().Be("disabled");
            result[0].Provider.Should().Be("openrouter-stt");
            result[0].Explanation.Should().Contain("OpenRouter API key");
            _audioIntroSegmentExtractor.Verify(x => x.Extract(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
            _httpClient.Verify(x => x.Post(It.IsAny<HttpRequest>()), Times.Never);
            _suggestionRepository.Verify(x => x.Insert(It.IsAny<UnmappedFileIdentificationSuggestion>()), Times.Never);
        }

        [Test]
        public void deep_identify_should_capture_transcript_with_openrouter_stt_json_audio()
        {
            HttpRequest postedRequest = null;

            _configService.SetupGet(x => x.SpeechToTextProvider).Returns("openrouter");
            _configService.SetupGet(x => x.OpenRouterEnabled).Returns(true);
            _configService.SetupGet(x => x.OpenRouterApiKey).Returns("openrouter-key");
            _configService.SetupGet(x => x.SpeechToTextModel).Returns(string.Empty);
            _audioIntroSegmentExtractor.Setup(x => x.Extract("/books/Alice Writer - Hidden.m4b", 30))
                .Returns(new AudioIntroSegment
                {
                    Status = "extracted",
                    FileName = "intro.mp3",
                    ContentType = "audio/mpeg",
                    Format = "mp3",
                    Content = Encoding.UTF8.GetBytes("audio bytes")
                });
            _httpClient.Setup(x => x.Post(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(r =>
                {
                    postedRequest = r;
                    return new HttpResponse(r, new HttpHeader { ContentType = "application/json" }, "{\"text\":\"You're listening to The Hidden Book, written by Alice Writer, narrated by Jane Reader.\"}");
                });

            var result = _subject.DeepIdentifyAudio(new List<BookFileResource>
            {
                new BookFileResource { Id = 1, Path = "/books/Alice Writer - Hidden.m4b" }
            });

            result.Should().ContainSingle();
            result[0].Status.Should().Be("transcriptCaptured");
            result[0].Provider.Should().Be("openrouter-stt");
            result[0].LikelyBook.Should().Be("The Hidden Book");
            result[0].LikelyAuthor.Should().Be("Alice Writer");
            result[0].Narrator.Should().Be("Jane Reader");
            result[0].Stage.Should().Be("transcriptCaptured");
            result[0].ProviderEndpoint.Should().Be("https://openrouter.ai/api/v1/audio/transcriptions");
            result[0].ProviderModel.Should().Be("openai/whisper-1");
            result[0].ProviderStatusCode.Should().Be(200);
            result[0].ProviderDurationMs.Should().NotBeNull();
            result[0].ProviderResponseExcerpt.Should().Contain("The Hidden Book");
            postedRequest.Should().NotBeNull();
            postedRequest.Url.FullUri.Should().Be("https://openrouter.ai/api/v1/audio/transcriptions");
            postedRequest.Headers.GetSingleValue("Authorization").Should().Be("Bearer openrouter-key");
            postedRequest.Headers.ContentType.Should().Be("application/json");
            var body = JObject.Parse(Encoding.UTF8.GetString(postedRequest.ContentData));
            body.Value<string>("model").Should().Be("openai/whisper-1");
            body["input_audio"].Value<string>("data").Should().Be("YXVkaW8gYnl0ZXM=");
            body["input_audio"].Value<string>("format").Should().Be("mp3");
            _suggestionRepository.Verify(x => x.Insert(It.Is<UnmappedFileIdentificationSuggestion>(s => s.BookFileId == 1 && s.Status == "transcriptCaptured" && s.Provider == "openrouter-stt" && s.Narrator == "Jane Reader" && s.Stage == "transcriptCaptured" && s.ProviderModel == "openai/whisper-1")), Times.Once);
        }

        [Test]
        public void deep_identify_should_capture_transcript_when_stt_provider_returns_text()
        {
            _configService.SetupGet(x => x.SpeechToTextProvider).Returns("openai-compatible");
            _configService.SetupGet(x => x.SpeechToTextApiKey).Returns("test-key");
            _audioIntroSegmentExtractor.Setup(x => x.Extract("/books/Alice Writer - Hidden.m4b", 30))
                .Returns(new AudioIntroSegment
                {
                    Status = "extracted",
                    FileName = "intro.mp3",
                    ContentType = "audio/mpeg",
                    Content = Encoding.UTF8.GetBytes("audio bytes")
                });
            _httpClient.Setup(x => x.Post(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(r => new HttpResponse(r, new HttpHeader { ContentType = "application/json" }, "{\"text\":\"You're listening to The Hidden Book, written by Alice Writer, narrated by Jane Reader, published by Example Audio. Book 2 of The Hidden Series.\"}"));

            var result = _subject.DeepIdentifyAudio(new List<BookFileResource>
            {
                new BookFileResource { Id = 1, Path = "/books/Alice Writer - Hidden.m4b" },
                new BookFileResource { Id = 2, Path = "/books/Alice Writer - Cover.jpg" }
            });

            result.Should().HaveCount(2);
            result[0].Status.Should().Be("transcriptCaptured");
            result[0].Provider.Should().Be("openai-compatible");
            result[0].LikelyBook.Should().Be("The Hidden Book");
            result[0].LikelyAuthor.Should().Be("Alice Writer");
            result[0].Narrator.Should().Be("Jane Reader");
            result[0].Confidence.Should().Be(100);
            result[0].TranscriptExcerpt.Should().Contain("The Hidden Book");
            result[0].RequiresManualConfirmation.Should().BeTrue();
            result[1].Status.Should().Be("disabled");
            _httpClient.Verify(x => x.Post(It.Is<HttpRequest>(r => r.Url.FullUri == "https://api.openai.com/v1/audio/transcriptions" && r.Headers.GetSingleValue("Authorization") == "Bearer test-key" && r.Headers.ContentType.StartsWith("multipart/form-data"))), Times.Once);
            _suggestionRepository.Verify(x => x.Insert(It.Is<UnmappedFileIdentificationSuggestion>(s => s.BookFileId == 1 && s.Status == "transcriptCaptured" && s.Provider == "openai-compatible" && s.Narrator == "Jane Reader")), Times.Once);
            _suggestionRepository.Verify(x => x.Insert(It.Is<UnmappedFileIdentificationSuggestion>(s => s.BookFileId == 2)), Times.Never);
        }

        [Test]
        public void deep_identify_should_return_safe_debug_details_when_provider_fails()
        {
            _configService.SetupGet(x => x.SpeechToTextProvider).Returns("openrouter");
            _configService.SetupGet(x => x.OpenRouterEnabled).Returns(true);
            _configService.SetupGet(x => x.OpenRouterApiKey).Returns("secret-key");
            _configService.SetupGet(x => x.SpeechToTextModel).Returns("openai/whisper-1");
            _audioIntroSegmentExtractor.Setup(x => x.Extract("/books/Alice Writer - Hidden.m4b", 30))
                .Returns(new AudioIntroSegment
                {
                    Status = "extracted",
                    FileName = "intro.mp3",
                    ContentType = "audio/mpeg",
                    Format = "mp3",
                    Content = Encoding.UTF8.GetBytes("audio bytes")
                });
            _httpClient.Setup(x => x.Post(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(r => new HttpResponse(r, new HttpHeader { ContentType = "application/json" }, "{\"error\":\"bad audio\"}", global::System.Net.HttpStatusCode.BadRequest));

            var result = _subject.DeepIdentifyAudio(new List<BookFileResource>
            {
                new BookFileResource { Id = 1, Path = "/books/Alice Writer - Hidden.m4b" }
            });

            result.Should().ContainSingle();
            result[0].Status.Should().Be("transcriptionFailed");
            result[0].Stage.Should().Be("transcriptionFailed");
            result[0].ProviderEndpoint.Should().Be("https://openrouter.ai/api/v1/audio/transcriptions");
            result[0].ProviderModel.Should().Be("openai/whisper-1");
            result[0].ProviderStatusCode.Should().Be(400);
            result[0].ProviderDurationMs.Should().NotBeNull();
            result[0].ProviderResponseExcerpt.Should().Contain("bad audio");
            result[0].ProviderResponseExcerpt.Should().NotContain("secret-key");
            _suggestionRepository.Verify(x => x.Insert(It.Is<UnmappedFileIdentificationSuggestion>(s => s.BookFileId == 1 && s.Status == "transcriptionFailed" && s.ProviderStatusCode == 400 && s.ProviderResponseExcerpt.Contains("bad audio"))), Times.Once);
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
        public void suggestion_evidence_should_warn_on_transcript_candidate_mismatch_without_warning_on_missing_narrator_metadata()
        {
            var review = new ManualImportReviewResource
            {
                Candidate = new ManualImportCandidateResource
                {
                    AuthorName = "Alice Writer",
                    BookTitle = "The Correct Book",
                    EditionTitle = "Audio Edition"
                },
                Suggestions = new List<ManualImportIdentificationSuggestionResource>
                {
                    new ManualImportIdentificationSuggestionResource
                    {
                        Type = "deepAudio",
                        Provider = "openrouter-stt",
                        Status = "transcriptCaptured",
                        LikelyAuthor = "Alice Writer",
                        LikelyBook = "The Wrong Book",
                        Narrator = "Jane Reader"
                    }
                }
            };

            ManualImportReviewResourceMapper.ApplySuggestionEvidence(review);

            review.Suggestions[0].Evidence.Should().Contain(x => x.Kind == "authorMatch");
            review.Suggestions[0].Evidence.Should().Contain(x => x.Kind == "narratorEvidence" && x.Detail.Contains("Jane Reader"));
            review.Suggestions[0].Warnings.Should().Contain(x => x.Kind == "bookMismatch");
            review.Suggestions[0].Warnings.Should().NotContain(x => x.Kind == "narratorMismatch");
        }

        [Test]
        public void suggestion_evidence_should_warn_when_narrator_sources_conflict()
        {
            var review = new ManualImportReviewResource
            {
                Candidate = new ManualImportCandidateResource
                {
                    AuthorName = "Alice Writer",
                    BookTitle = "The Hidden Book"
                },
                Suggestions = new List<ManualImportIdentificationSuggestionResource>
                {
                    new ManualImportIdentificationSuggestionResource
                    {
                        Type = "deepAudio",
                        Provider = "openrouter-stt",
                        Status = "transcriptCaptured",
                        LikelyAuthor = "Alice Writer",
                        LikelyBook = "The Hidden Book",
                        Narrator = "Jane Reader"
                    },
                    new ManualImportIdentificationSuggestionResource
                    {
                        Type = "aiReview",
                        Provider = "openrouter",
                        Status = "suggested",
                        LikelyAuthor = "Alice Writer",
                        LikelyBook = "The Hidden Book",
                        Narrator = "Different Narrator"
                    }
                }
            };

            ManualImportReviewResourceMapper.ApplySuggestionEvidence(review);

            review.Suggestions[0].Warnings.Should().Contain(x => x.Kind == "narratorMismatch");
            review.Suggestions[1].Warnings.Should().Contain(x => x.Kind == "narratorMismatch");
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
                        Stage = "transcriptCaptured",
                        ProviderEndpoint = "https://openrouter.ai/api/v1/audio/transcriptions",
                        ProviderModel = "openai/whisper-1",
                        ProviderStatusCode = 200,
                        ProviderDurationMs = 123,
                        ProviderResponseExcerpt = "{\"text\":\"Hidden\"}",
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
            result[0].Stage.Should().Be("transcriptCaptured");
            result[0].ProviderDurationMs.Should().Be(123);
            result[0].ProviderResponseExcerpt.Should().Contain("Hidden");
        }
    }
}
