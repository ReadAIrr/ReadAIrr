using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;

namespace Readarr.Api.V1.BookFiles
{
    public interface IAudioIntroTranscriptionService
    {
        AudioIntroTranscriptionResult Prepare(BookFileResource resource);
        AudioIntroTranscriptClues ExtractClues(string transcript);
    }

    public class AudioIntroTranscriptionResult
    {
        public string Provider { get; set; }
        public string Status { get; set; }
        public string Explanation { get; set; }
        public string TranscriptExcerpt { get; set; }
        public string ContextSummary { get; set; }
        public int IntroSeconds { get; set; }
        public AudioIntroTranscriptClues Clues { get; set; }
    }

    public class AudioIntroTranscriptClues
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public string Narrator { get; set; }
        public string Publisher { get; set; }
        public string Series { get; set; }
        public int Confidence { get; set; }
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
                    Clues = new AudioIntroTranscriptClues(),
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
                    Clues = new AudioIntroTranscriptClues(),
                    Explanation = "Speech-to-text provider is selected, but its API key is missing.",
                    ContextSummary = "No audio or transcript was sent. Add the provider key to enable user-triggered intro transcription."
                };
            }

            return new AudioIntroTranscriptionResult
            {
                Provider = provider,
                Status = "providerReady",
                IntroSeconds = _configService.SpeechToTextIntroSeconds,
                Clues = new AudioIntroTranscriptClues(),
                Explanation = "Speech-to-text provider configuration is present. Intro extraction/transcription is staged behind this provider boundary and remains manual-triggered only.",
                ContextSummary = $"Provider boundary ready for a short intro window of up to {_configService.SpeechToTextIntroSeconds} seconds. No automatic import, tag write, file move, or background transcription will run."
            };
        }

        public AudioIntroTranscriptClues ExtractClues(string transcript)
        {
            var normalized = Regex.Replace(transcript ?? string.Empty, @"\s+", " ").Trim();
            var clues = new AudioIntroTranscriptClues();

            if (normalized.IsNullOrWhiteSpace())
            {
                return clues;
            }

            clues.Title = FirstMatch(normalized,
                @"(?:you(?: are|'re) listening to|this is|welcome to)\s+(?<value>.+?)(?:,?\s+(?:written by|by|narrated by|read by|performed by)\b|[.])",
                @"(?<value>.+?)\s*,?\s+(?:written by|by)\s+");

            clues.Author = FirstMatch(normalized,
                @"(?:written by|by)\s+(?<value>.+?)(?:,?\s+(?:narrated by|read by|performed by|published by)\b|[.])");

            clues.Narrator = FirstMatch(normalized,
                @"(?:narrated by|read by|performed by)\s+(?<value>.+?)(?:,?\s+(?:published by|from)\b|[.])");

            clues.Publisher = FirstMatch(normalized,
                @"(?:published by|from)\s+(?<value>.+?)(?:[.])");

            clues.Series = FirstMatch(normalized,
                new[] { @"(?:book|part|volume)\s+(?<value>\d+)\s+(?:of|in)\s+(?<series>.+?)(?:[.])" },
                "series");

            clues.Confidence = new[] { clues.Title, clues.Author, clues.Narrator, clues.Publisher, clues.Series }.Count(x => x.IsNotNullOrWhiteSpace()) * 20;

            return clues;
        }

        private static string FirstMatch(string transcript, params string[] patterns)
        {
            return FirstMatch(transcript, patterns, "value");
        }

        private static string FirstMatch(string transcript, string[] patterns, string groupName)
        {
            foreach (var pattern in patterns)
            {
                var match = Regex.Match(transcript, pattern, RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    return Clean(match.Groups[groupName].Value);
                }
            }

            return null;
        }

        private static string Clean(string value)
        {
            return value?.Trim(' ', ',', '.', '"', '\'');
        }
    }
}
