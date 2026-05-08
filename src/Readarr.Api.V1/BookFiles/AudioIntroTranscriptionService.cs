using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;

namespace Readarr.Api.V1.BookFiles
{
    public interface IAudioIntroTranscriptionService
    {
        AudioIntroTranscriptionResult Prepare(BookFileResource resource);
    }

    public class AudioIntroTranscriptionResult
    {
        public string Provider { get; set; }
        public string Status { get; set; }
        public string Explanation { get; set; }
        public string TranscriptExcerpt { get; set; }
        public string ContextSummary { get; set; }
        public int IntroSeconds { get; set; }
    }

    public class AudioIntroTranscriptionService : IAudioIntroTranscriptionService
    {
        private readonly IConfigService _configService;

        public AudioIntroTranscriptionService(IConfigService configService)
        {
            _configService = configService;
        }

        public AudioIntroTranscriptionResult Prepare(BookFileResource resource)
        {
            var provider = _configService.SpeechToTextProvider;

            if (provider.IsNullOrWhiteSpace() || provider == "disabled")
            {
                return new AudioIntroTranscriptionResult
                {
                    Provider = "disabled",
                    Status = "disabled",
                    IntroSeconds = _configService.SpeechToTextIntroSeconds,
                    Explanation = "Speech-to-text provider is disabled. Configure a provider before Deep Identify Audio can transcribe intro evidence.",
                    ContextSummary = "No audio or transcript was sent. Deep Identify Audio remains a manual review-only action."
                };
            }

            if (_configService.SpeechToTextApiKey.IsNullOrWhiteSpace())
            {
                return new AudioIntroTranscriptionResult
                {
                    Provider = provider,
                    Status = "disabled",
                    IntroSeconds = _configService.SpeechToTextIntroSeconds,
                    Explanation = "Speech-to-text provider is selected, but its API key is missing.",
                    ContextSummary = "No audio or transcript was sent. Add the provider key to enable user-triggered intro transcription."
                };
            }

            return new AudioIntroTranscriptionResult
            {
                Provider = provider,
                Status = "providerReady",
                IntroSeconds = _configService.SpeechToTextIntroSeconds,
                Explanation = "Speech-to-text provider configuration is present. Intro extraction/transcription is staged behind this provider boundary and remains manual-triggered only.",
                ContextSummary = $"Provider boundary ready for a short intro window of up to {_configService.SpeechToTextIntroSeconds} seconds. No automatic import, tag write, file move, or background transcription will run."
            };
        }
    }
}
