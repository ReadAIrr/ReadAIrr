using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Processes;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Configuration;

namespace Readarr.Api.V1.BookFiles
{
    public interface IAudioIntroTranscriptionService
    {
        AudioIntroTranscriptionResult Prepare(BookFileResource resource);
        AudioIntroSegment ExtractPreview(BookFileResource resource);
        AudioIntroTranscriptionResult Transcribe(BookFileResource resource);
        AudioIntroTranscriptClues ExtractClues(string transcript);
    }

    public interface IAudioIntroSegmentExtractor
    {
        AudioIntroSegment Extract(string path, int introSeconds);
    }

    public class AudioIntroSegment
    {
        public string Status { get; set; }
        public string Explanation { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public string Format { get; set; }
        public byte[] Content { get; set; }

        public bool IsSuccess => Status == "extracted" && Content != null && Content.Length > 0;
    }

    public class AudioIntroTranscriptionResult
    {
        public string Provider { get; set; }
        public string Status { get; set; }
        public string Explanation { get; set; }
        public string Transcript { get; set; }
        public bool TranscriptIsTruncated { get; set; }
        public string TranscriptExcerpt { get; set; }
        public string ContextSummary { get; set; }
        public int IntroSeconds { get; set; }
        public string Stage { get; set; }
        public string ProviderEndpoint { get; set; }
        public string ProviderModel { get; set; }
        public int? ProviderStatusCode { get; set; }
        public int? ProviderDurationMs { get; set; }
        public string ProviderResponseExcerpt { get; set; }
        public AudioIntroTranscriptClues Clues { get; set; }
        public List<AudioIntroTranscriptionStep> Steps { get; set; }
    }

    public class AudioIntroTranscriptionStep
    {
        public string Kind { get; set; }
        public string Label { get; set; }
        public string Detail { get; set; }
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
        private const int MaxIntroSeconds = 60;
        private const int MaxTranscriptLength = 12000;

        private readonly IConfigService _configService;
        private readonly IHttpClient _httpClient;
        private readonly IAudioIntroSegmentExtractor _audioIntroSegmentExtractor;

        public AudioIntroTranscriptionService(IConfigService configService, IHttpClient httpClient, IAudioIntroSegmentExtractor audioIntroSegmentExtractor)
        {
            _configService = configService;
            _httpClient = httpClient;
            _audioIntroSegmentExtractor = audioIntroSegmentExtractor;
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
                    Stage = "disabled",
                    IntroSeconds = _configService.SpeechToTextIntroSeconds,
                    Clues = new AudioIntroTranscriptClues(),
                    Explanation = "Speech-to-text provider is disabled. Configure a provider before Deep Identify Audio can transcribe intro evidence.",
                    ContextSummary = "No audio or transcript was sent. Deep Identify Audio remains a manual review-only action.",
                    Steps = new List<AudioIntroTranscriptionStep>
                    {
                        Step("disabled", "Provider disabled", "No audio or transcript was sent because speech-to-text is disabled.")
                    }
                };
            }

            if (provider == "openrouter")
            {
                if (!_configService.OpenRouterEnabled)
                {
                    return new AudioIntroTranscriptionResult
                    {
                        Provider = "openrouter-stt",
                        Status = "disabled",
                        Stage = "disabled",
                        IntroSeconds = _configService.SpeechToTextIntroSeconds,
                        Clues = new AudioIntroTranscriptClues(),
                        Explanation = "OpenRouter speech-to-text is selected, but OpenRouter is disabled.",
                        ContextSummary = "No audio or transcript was sent. Enable OpenRouter and add its API key before using Deep Identify Audio with OpenRouter STT.",
                        Steps = new List<AudioIntroTranscriptionStep>
                        {
                            Step("disabled", "OpenRouter disabled", "No audio or transcript was sent because OpenRouter is disabled.")
                        }
                    };
                }

                if (_configService.OpenRouterApiKey.IsNullOrWhiteSpace())
                {
                    return new AudioIntroTranscriptionResult
                    {
                        Provider = "openrouter-stt",
                        Status = "disabled",
                        Stage = "disabled",
                        IntroSeconds = _configService.SpeechToTextIntroSeconds,
                        Clues = new AudioIntroTranscriptClues(),
                        Explanation = "OpenRouter speech-to-text is selected, but the OpenRouter API key is missing.",
                        ContextSummary = "No audio or transcript was sent. OpenRouter STT shares the same BYO OpenRouter API key used by AI Review.",
                        Steps = new List<AudioIntroTranscriptionStep>
                        {
                            Step("disabled", "OpenRouter key missing", "No audio or transcript was sent because the OpenRouter API key is missing.")
                        }
                    };
                }

                return new AudioIntroTranscriptionResult
                {
                    Provider = "openrouter-stt",
                    Status = "providerReady",
                    Stage = "providerReady",
                    IntroSeconds = _configService.SpeechToTextIntroSeconds,
                    ProviderEndpoint = BuildEndpointSummary(_configService.OpenRouterBaseUrl, "audio/transcriptions"),
                    ProviderModel = GetSpeechToTextModel("openai/whisper-1"),
                    Clues = new AudioIntroTranscriptClues(),
                    Explanation = "OpenRouter speech-to-text configuration is present. Intro extraction/transcription remains manual-triggered only.",
                    ContextSummary = $"OpenRouter STT will receive only a short intro window of up to {_configService.SpeechToTextIntroSeconds} seconds. No automatic import, tag write, file move, or background transcription will run.",
                    Steps = new List<AudioIntroTranscriptionStep>
                    {
                        Step("providerReady", "Provider ready", "OpenRouter speech-to-text is configured; audio is sent only after a user-triggered Deep Identify action.")
                    }
                };
            }

            if (_configService.SpeechToTextApiKey.IsNullOrWhiteSpace())
            {
                return new AudioIntroTranscriptionResult
                {
                    Provider = provider,
                    Status = "disabled",
                    Stage = "disabled",
                    IntroSeconds = _configService.SpeechToTextIntroSeconds,
                    Clues = new AudioIntroTranscriptClues(),
                    Explanation = "Speech-to-text provider is selected, but its API key is missing.",
                    ContextSummary = "No audio or transcript was sent. Add the provider key to enable user-triggered intro transcription.",
                    Steps = new List<AudioIntroTranscriptionStep>
                    {
                        Step("disabled", "Provider key missing", "No audio or transcript was sent because the speech-to-text API key is missing.")
                    }
                };
            }

            return new AudioIntroTranscriptionResult
            {
                Provider = provider,
                Status = "providerReady",
                Stage = "providerReady",
                IntroSeconds = _configService.SpeechToTextIntroSeconds,
                ProviderEndpoint = BuildEndpointSummary(_configService.SpeechToTextBaseUrl, "audio/transcriptions"),
                ProviderModel = GetSpeechToTextModel("whisper-1"),
                Clues = new AudioIntroTranscriptClues(),
                Explanation = "Speech-to-text provider configuration is present. Intro extraction/transcription is staged behind this provider boundary and remains manual-triggered only.",
                ContextSummary = $"Provider boundary ready for a short intro window of up to {_configService.SpeechToTextIntroSeconds} seconds. No automatic import, tag write, file move, or background transcription will run.",
                Steps = new List<AudioIntroTranscriptionStep>
                {
                    Step("providerReady", "Provider ready", "Speech-to-text is configured; audio is sent only after a user-triggered Deep Identify action.")
                }
            };
        }

        public AudioIntroSegment ExtractPreview(BookFileResource resource)
        {
            var introSeconds = BoundIntroSeconds(_configService.SpeechToTextIntroSeconds);
            return _audioIntroSegmentExtractor.Extract(resource.Path, introSeconds);
        }

        public AudioIntroTranscriptionResult Transcribe(BookFileResource resource)
        {
            var ready = Prepare(resource);

            if (ready.Status != "providerReady")
            {
                return ready;
            }

            var introSeconds = BoundIntroSeconds(_configService.SpeechToTextIntroSeconds);
            var steps = new List<AudioIntroTranscriptionStep>
            {
                Step("extractingIntro", "Extracting intro clip", $"Running ffmpeg locally against the selected file and limiting extraction to the first {introSeconds} seconds.")
            };
            var segment = ExtractPreview(resource);

            if (!segment.IsSuccess)
            {
                steps.Add(Step(segment.Status, "Intro extraction failed", segment.Explanation));

                return new AudioIntroTranscriptionResult
                {
                    Provider = ready.Provider,
                    Status = segment.Status,
                    Stage = segment.Status,
                    IntroSeconds = introSeconds,
                    Clues = new AudioIntroTranscriptClues(),
                    Explanation = segment.Explanation,
                    ContextSummary = "No audio was sent to the speech-to-text provider because intro extraction did not complete.",
                    Steps = steps
                };
            }

            steps.Add(Step("introClipReady", "Intro clip ready", $"Prepared a {segment.Format ?? "mp3"} intro clip with {segment.Content.Length} bytes for manual review transcription."));

            try
            {
                steps.Add(Step("providerRequest", "Provider request", $"Sending bounded intro audio to {ready.Provider}; request body contains model plus the extracted audio clip only."));
                var response = SendTranscriptionRequest(segment, introSeconds);
                steps.Add(Step("providerResponse", "Provider response", $"Received HTTP {(int)response.Response.StatusCode} from {response.Endpoint} model {response.Model} in {response.DurationMs} ms."));
                var transcript = ExtractTranscript(response.Response.Content);
                var transcriptForReview = Truncate(transcript, MaxTranscriptLength);
                var excerpt = Truncate(transcript, 1000);
                steps.Add(Step("parsingTranscript", "Parsing transcript", $"Parsed {transcript.Length} transcript characters for title, author, narrator, publisher, and series clues."));
                var clues = ExtractClues(transcript);
                steps.Add(Step("evidenceCreation", "Evidence created", BuildEvidenceSummary(clues)));
                steps.Add(Step("suggestionCreation", "Suggestion created", "Created a review-only Deep Identify suggestion. Nothing was imported, moved, renamed, tagged, monitored, searched, or downloaded."));
                steps.Add(Step("final", "Final state", "Transcript captured for manual confirmation."));

                return new AudioIntroTranscriptionResult
                {
                    Provider = ready.Provider,
                    Status = "transcriptCaptured",
                    Stage = "transcriptCaptured",
                    IntroSeconds = introSeconds,
                    Transcript = transcriptForReview,
                    TranscriptIsTruncated = transcript.Length > MaxTranscriptLength,
                    TranscriptExcerpt = excerpt,
                    ProviderEndpoint = response.Endpoint,
                    ProviderModel = response.Model,
                    ProviderStatusCode = (int)response.Response.StatusCode,
                    ProviderDurationMs = response.DurationMs,
                    ProviderResponseExcerpt = Truncate(response.Response.Content, 1000),
                    Clues = clues,
                    Explanation = "Speech-to-text captured a short intro transcript for manual review.",
                    ContextSummary = $"Captured transcript evidence from the first {introSeconds} seconds only. This remains review-only and did not import, rename, retag, or move the file.",
                    Steps = steps
                };
            }
            catch (TranscriptionProviderException ex)
            {
                steps.Add(Step("providerResponse", "Provider response", $"Provider returned HTTP {ex.StatusCode} from {ex.Endpoint} model {ex.Model} in {ex.DurationMs} ms."));
                steps.Add(Step("error", "Transcription failed", ex.Message));

                return new AudioIntroTranscriptionResult
                {
                    Provider = ready.Provider,
                    Status = "transcriptionFailed",
                    Stage = "transcriptionFailed",
                    IntroSeconds = introSeconds,
                    ProviderEndpoint = ex.Endpoint,
                    ProviderModel = ex.Model,
                    ProviderStatusCode = ex.StatusCode,
                    ProviderDurationMs = ex.DurationMs,
                    ProviderResponseExcerpt = ex.ResponseExcerpt,
                    Clues = new AudioIntroTranscriptClues(),
                    Explanation = ex.Message,
                    ContextSummary = "The selected speech-to-text provider failed. No import, rename, tag write, or file move was performed.",
                    Steps = steps
                };
            }
            catch (Exception ex)
            {
                steps.Add(Step("error", "Transcription failed", ex.Message));

                return new AudioIntroTranscriptionResult
                {
                    Provider = ready.Provider,
                    Status = "transcriptionFailed",
                    Stage = "transcriptionFailed",
                    IntroSeconds = introSeconds,
                    Clues = new AudioIntroTranscriptClues(),
                    Explanation = ex.Message,
                    ContextSummary = "The selected speech-to-text provider failed. No import, rename, tag write, or file move was performed.",
                    Steps = steps
                };
            }
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

        private TranscriptionHttpResult SendTranscriptionRequest(AudioIntroSegment segment, int introSeconds)
        {
            if (_configService.SpeechToTextProvider == "openrouter")
            {
                return SendOpenRouterTranscriptionRequest(segment, introSeconds);
            }

            return SendOpenAiCompatibleTranscriptionRequest(segment, introSeconds);
        }

        private TranscriptionHttpResult SendOpenRouterTranscriptionRequest(AudioIntroSegment segment, int introSeconds)
        {
            var baseUrl = _configService.OpenRouterBaseUrl.IsNotNullOrWhiteSpace() ? _configService.OpenRouterBaseUrl.TrimEnd('/') : "https://openrouter.ai/api/v1";
            var model = GetSpeechToTextModel("openai/whisper-1");
            const string resource = "audio/transcriptions";
            var request = new HttpRequestBuilder(baseUrl)
            {
                Method = HttpMethod.Post,
                SuppressHttpError = true,
                LogResponseContent = false
            }
                .Resource(resource)
                .Build();

            request.Headers.Set("Authorization", $"Bearer {_configService.OpenRouterApiKey}");
            request.Headers.Set("Content-Type", "application/json");
            request.RequestTimeout = TimeSpan.FromSeconds(Math.Max(30, introSeconds + 30));
            request.SetContent(new
            {
                model,
                input_audio = new
                {
                    data = Convert.ToBase64String(segment.Content),
                    format = segment.Format ?? "mp3"
                }
            }.ToJson());
            request.ContentSummary = $"OpenRouter speech-to-text request with {segment.Content.Length} bytes from first {introSeconds} seconds of selected audio";

            return PostTranscriptionRequest(request, BuildEndpointSummary(baseUrl, resource), model);
        }

        private TranscriptionHttpResult SendOpenAiCompatibleTranscriptionRequest(AudioIntroSegment segment, int introSeconds)
        {
            var baseUrl = _configService.SpeechToTextBaseUrl.IsNotNullOrWhiteSpace() ? _configService.SpeechToTextBaseUrl.TrimEnd('/') : "https://api.openai.com/v1";
            var model = GetSpeechToTextModel("whisper-1");
            const string resource = "audio/transcriptions";
            var request = new HttpRequestBuilder(baseUrl)
            {
                Method = HttpMethod.Post,
                SuppressHttpError = true,
                LogResponseContent = false
            }
                .Resource(resource)
                .AddFormParameter("model", model)
                .AddFormUpload("file", segment.FileName, segment.Content, segment.ContentType)
                .Build();

            request.Headers.Set("Authorization", $"Bearer {_configService.SpeechToTextApiKey}");
            request.RequestTimeout = TimeSpan.FromSeconds(Math.Max(30, introSeconds + 30));
            request.ContentSummary = $"Speech-to-text transcription request with {segment.Content.Length} bytes from first {introSeconds} seconds of selected audio";

            return PostTranscriptionRequest(request, BuildEndpointSummary(baseUrl, resource), model);
        }

        private TranscriptionHttpResult PostTranscriptionRequest(HttpRequest request, string endpoint, string model)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = _httpClient.Post(request);
            stopwatch.Stop();

            if (response.HasHttpError)
            {
                var responseExcerpt = Truncate(response.Content, 1000);

                throw new TranscriptionProviderException($"Speech-to-text provider returned {(int)response.StatusCode}: {Truncate(response.Content, 500)}")
                {
                    Endpoint = endpoint,
                    Model = model,
                    StatusCode = (int)response.StatusCode,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds,
                    ResponseExcerpt = responseExcerpt
                };
            }

            return new TranscriptionHttpResult
            {
                Response = response,
                Endpoint = endpoint,
                Model = model,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }

        private string GetSpeechToTextModel(string defaultModel)
        {
            if (_configService.SpeechToTextProvider == "openrouter" && _configService.SpeechToTextModel == "whisper-1")
            {
                return defaultModel;
            }

            return _configService.SpeechToTextModel.IsNotNullOrWhiteSpace() ? _configService.SpeechToTextModel : defaultModel;
        }

        private static string ExtractTranscript(string responseContent)
        {
            var json = JObject.Parse(responseContent);
            var text = json.Value<string>("text");

            if (text.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException("Speech-to-text provider response did not include transcript text.");
            }

            return text;
        }

        private static int BoundIntroSeconds(int introSeconds)
        {
            return Math.Min(MaxIntroSeconds, Math.Max(1, introSeconds));
        }

        private static string Truncate(string value, int maxLength)
        {
            if (value == null || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength);
        }

        private static AudioIntroTranscriptionStep Step(string kind, string label, string detail)
        {
            return new AudioIntroTranscriptionStep
            {
                Kind = kind,
                Label = label,
                Detail = detail
            };
        }

        private static string BuildEvidenceSummary(AudioIntroTranscriptClues clues)
        {
            var parts = new[]
            {
                clues?.Author.IsNotNullOrWhiteSpace() == true ? $"author '{clues.Author}'" : null,
                clues?.Title.IsNotNullOrWhiteSpace() == true ? $"book '{clues.Title}'" : null,
                clues?.Narrator.IsNotNullOrWhiteSpace() == true ? $"narrator '{clues.Narrator}'" : null,
                clues?.Publisher.IsNotNullOrWhiteSpace() == true ? $"publisher '{clues.Publisher}'" : null,
                clues?.Series.IsNotNullOrWhiteSpace() == true ? $"series '{clues.Series}'" : null
            }.Where(x => x.IsNotNullOrWhiteSpace()).ToList();

            if (!parts.Any())
            {
                return "No strong title, author, narrator, publisher, or series clues were detected.";
            }

            return $"Detected {parts.ConcatToString(", ")}.";
        }

        private static string BuildEndpointSummary(string baseUrl, string resource)
        {
            return $"{(baseUrl.IsNotNullOrWhiteSpace() ? baseUrl.TrimEnd('/') : "https://api.openai.com/v1")}/{resource}";
        }

        private sealed class TranscriptionHttpResult
        {
            public HttpResponse Response { get; set; }
            public string Endpoint { get; set; }
            public string Model { get; set; }
            public int DurationMs { get; set; }
        }

        private sealed class TranscriptionProviderException : Exception
        {
            public TranscriptionProviderException(string message)
                : base(message)
            {
            }

            public string Endpoint { get; set; }
            public string Model { get; set; }
            public int StatusCode { get; set; }
            public int DurationMs { get; set; }
            public string ResponseExcerpt { get; set; }
        }
    }

    public class AudioIntroSegmentExtractor : IAudioIntroSegmentExtractor
    {
        private readonly IProcessProvider _processProvider;

        public AudioIntroSegmentExtractor(IProcessProvider processProvider)
        {
            _processProvider = processProvider;
        }

        public AudioIntroSegment Extract(string path, int introSeconds)
        {
            if (!File.Exists(path))
            {
                return Failed("extractionFailed", "Audio file no longer exists at the selected path.");
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"readairr-intro-{Guid.NewGuid():N}.mp3");

            try
            {
                var output = _processProvider.StartAndCapture("ffmpeg", $"-y -hide_banner -loglevel error -t {introSeconds} -i {Quote(path)} -vn -ac 1 -ar 16000 -f mp3 {Quote(tempPath)}");

                if (output.ExitCode != 0 || !File.Exists(tempPath))
                {
                    var error = output.Error.Select(x => x.Content).ConcatToString(" ");
                    return Failed("extractionFailed", $"Unable to extract short audio intro with ffmpeg. {error}".Trim());
                }

                var bytes = File.ReadAllBytes(tempPath);

                if (bytes.Length == 0)
                {
                    return Failed("extractionFailed", "Short audio intro extraction produced an empty segment.");
                }

                return new AudioIntroSegment
                {
                    Status = "extracted",
                    Explanation = $"Extracted the first {introSeconds} seconds for user-triggered speech-to-text review.",
                    FileName = Path.GetFileName(tempPath),
                    ContentType = "audio/mpeg",
                    Format = "mp3",
                    Content = bytes
                };
            }
            catch (Exception ex)
            {
                return Failed("extractionFailed", $"Unable to extract short audio intro with ffmpeg. {ex.Message}");
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    // Best-effort cleanup. A failed temp delete should not hide the review result.
                }
            }
        }

        private static AudioIntroSegment Failed(string status, string explanation)
        {
            return new AudioIntroSegment
            {
                Status = status,
                Explanation = explanation
            };
        }

        private static string Quote(string value)
        {
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }
    }
}
