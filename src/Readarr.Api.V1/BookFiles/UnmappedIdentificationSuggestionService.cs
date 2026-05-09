using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using Prowlarr.Api.V1.Config;
using Readarr.Api.V1.ManualImport;

namespace Readarr.Api.V1.BookFiles
{
    public interface IUnmappedIdentificationSuggestionService
    {
        OpenRouterConfigTestResource Test(OpenRouterConfigTestResource resource);
        List<ManualImportIdentificationSuggestionResource> ReviewWithAi(List<BookFileResource> resources);
        List<ManualImportIdentificationSuggestionResource> DeepIdentifyAudio(List<BookFileResource> resources);
        List<ManualImportIdentificationSuggestionResource> QueueDeepIdentifyAudio(List<BookFileResource> resources);
        List<ManualImportIdentificationSuggestionResource> ProcessQueuedDeepIdentifyAudio(List<BookFileResource> resources);
        List<ManualImportIdentificationSuggestionResource> GetPersisted(List<BookFileResource> resources);
        List<int> GetBookFileIdsMatchingTerm(string term);
        void Clear(List<int> bookFileIds);
    }

    public class UnmappedIdentificationSuggestionService : IUnmappedIdentificationSuggestionService
    {
        private static readonly HashSet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".aac",
            ".aiff",
            ".flac",
            ".m4a",
            ".m4b",
            ".mp3",
            ".ogg",
            ".opus",
            ".wav",
            ".wma"
        };

        private readonly IConfigService _configService;
        private readonly IHttpClient _httpClient;
        private readonly IAudioIntroTranscriptionService _audioIntroTranscriptionService;
        private readonly IUnmappedFileIdentificationSuggestionRepository _suggestionRepository;
        private readonly IContributorEvidenceRepository _contributorEvidenceRepository;
        private readonly Logger _logger;

        public UnmappedIdentificationSuggestionService(IConfigService configService,
                                                       IHttpClient httpClient,
                                                       IAudioIntroTranscriptionService audioIntroTranscriptionService,
                                                       IUnmappedFileIdentificationSuggestionRepository suggestionRepository,
                                                       IContributorEvidenceRepository contributorEvidenceRepository,
                                                       Logger logger)
        {
            _configService = configService;
            _httpClient = httpClient;
            _audioIntroTranscriptionService = audioIntroTranscriptionService;
            _suggestionRepository = suggestionRepository;
            _contributorEvidenceRepository = contributorEvidenceRepository;
            _logger = logger;
        }

        public OpenRouterConfigTestResource Test(OpenRouterConfigTestResource resource)
        {
            var config = BuildConfig(resource);

            if (!config.Enabled)
            {
                return Failed(resource, "OpenRouter is disabled.", "Enable OpenRouter before testing AI-assisted identification.");
            }

            if (config.ApiKey.IsNullOrWhiteSpace())
            {
                return Failed(resource, "OpenRouter API key is missing.", "Add a BYO OpenRouter API key to enable AI Review and Deep Identify actions.");
            }

            if (!config.BaseUrl.IsValidUrl())
            {
                return Failed(resource, "OpenRouter base URL is invalid.", "Use a full HTTPS URL, for example https://openrouter.ai/api/v1.");
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = SendChatCompletion(config, "Return this exact JSON: {\"ok\":true}", maxTokens: 20);
                stopwatch.Stop();

                return new OpenRouterConfigTestResource
                {
                    OpenRouterEnabled = resource.OpenRouterEnabled,
                    OpenRouterBaseUrl = config.BaseUrl,
                    OpenRouterModel = config.Model,
                    OpenRouterTimeout = config.Timeout,
                    OpenRouterMaxFileContext = config.MaxFileContext,
                    IsHealthy = (int)response.StatusCode >= 200 && (int)response.StatusCode < 300,
                    Message = (int)response.StatusCode >= 200 && (int)response.StatusCode < 300 ? "OpenRouter test completed." : "OpenRouter test failed.",
                    Detail = (int)response.StatusCode >= 200 && (int)response.StatusCode < 300 ? "The configured model responded to a minimal structured prompt." : Truncate(response.Content, 500),
                    StatusCode = (int)response.StatusCode,
                    ResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.Warn(ex, "OpenRouter test failed for {0} using model {1}", config.BaseUrl, config.Model);

                return new OpenRouterConfigTestResource
                {
                    OpenRouterEnabled = resource.OpenRouterEnabled,
                    OpenRouterBaseUrl = config.BaseUrl,
                    OpenRouterModel = config.Model,
                    OpenRouterTimeout = config.Timeout,
                    OpenRouterMaxFileContext = config.MaxFileContext,
                    IsHealthy = false,
                    Message = "OpenRouter test failed.",
                    Detail = ex.Message,
                    ResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds
                };
            }
        }

        public List<ManualImportIdentificationSuggestionResource> ReviewWithAi(List<BookFileResource> resources)
        {
            var config = BuildConfig();

            if (!config.Enabled || config.ApiKey.IsNullOrWhiteSpace())
            {
                return resources.Select(x => DisabledSuggestion("aiReview", x.Path, "OpenRouter is not configured.")).ToList();
            }

            var boundedResources = resources.Take(config.MaxFileContext).ToList();
            var prompt = BuildAiReviewPrompt(boundedResources);

            _logger.Info("Sending AI unmapped review request for {0} files to {1} model {2}. Context includes path/name, parsed tags, candidate summary, and rejection reasons only.",
                boundedResources.Count,
                config.BaseUrl,
                config.Model);

            try
            {
                var response = SendChatCompletion(config, prompt, maxTokens: 1200);
                var content = ExtractMessageContent(response.Content);
                var suggestions = ParseSuggestions(content, boundedResources);

                var result = resources.Select(resource =>
                {
                    var suggestion = suggestions.FirstOrDefault(x => PathEquals(x.Path, resource.Path));
                    return suggestion ?? StatusSuggestion("aiReview", resource.Path, "unavailable", "AI provider did not return a suggestion for this file.");
                }).ToList();

                Store(resources, result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "AI unmapped review failed");
                var result = resources.Select(x => StatusSuggestion("aiReview", x.Path, "failed", ex.Message)).ToList();
                Store(resources, result);

                return result;
            }
        }

        public List<ManualImportIdentificationSuggestionResource> DeepIdentifyAudio(List<BookFileResource> resources)
        {
            var result = resources.Select(DeepIdentifyResource).ToList();

            Store(resources, result);

            return result;
        }

        public List<ManualImportIdentificationSuggestionResource> QueueDeepIdentifyAudio(List<BookFileResource> resources)
        {
            Clear(resources.Select(x => x.Id).ToList());

            var result = resources.Select(resource =>
            {
                if (!IsAudioFile(resource.Path))
                {
                    return StatusSuggestion("deepAudio", resource.Path, "skipped", "File extension is not recognized as audio.");
                }

                return StatusSuggestion("deepAudio", resource.Path, "queued", "Deep Identify Audio is queued for background processing.");
            }).ToList();

            Store(resources, result, true);

            return result;
        }

        public List<ManualImportIdentificationSuggestionResource> ProcessQueuedDeepIdentifyAudio(List<BookFileResource> resources)
        {
            var result = new List<ManualImportIdentificationSuggestionResource>();

            foreach (var resource in resources)
            {
                if (!IsAudioFile(resource.Path))
                {
                    var skipped = StatusSuggestion("deepAudio", resource.Path, "skipped", "File extension is not recognized as audio.");
                    Store(new List<BookFileResource> { resource }, new List<ManualImportIdentificationSuggestionResource> { skipped }, true);
                    result.Add(skipped);
                    continue;
                }

                var running = StatusSuggestion("deepAudio", resource.Path, "extractingIntro", "Deep Identify Audio is extracting the bounded intro clip.");
                running.Stage = "extractingIntro";
                running.Steps = new List<ManualImportReviewReasonResource>
                {
                    Step("queued", "Queued", "Deep Identify Audio was queued for background processing."),
                    Step("extractingIntro", "Extracting intro clip", "Preparing the bounded local intro clip before sending anything to the configured provider.")
                };
                Store(new List<BookFileResource> { resource }, new List<ManualImportIdentificationSuggestionResource> { running }, true);

                var suggestion = DeepIdentifyResource(resource);
                suggestion.Steps = MergeQueuedSteps(suggestion.Steps);
                Store(new List<BookFileResource> { resource }, new List<ManualImportIdentificationSuggestionResource> { suggestion }, true);
                result.Add(suggestion);
            }

            return result;
        }

        public List<ManualImportIdentificationSuggestionResource> GetPersisted(List<BookFileResource> resources)
        {
            var byId = resources.ToDictionary(x => x.Id);

            return _suggestionRepository.GetByBookFileIds(byId.Keys)
                .Select(suggestion =>
                {
                    if (!byId.TryGetValue(suggestion.BookFileId, out var resource))
                    {
                        return null;
                    }

                    return ToResource(suggestion, resource);
                })
                .Where(x => x != null)
                .ToList();
        }

        public List<int> GetBookFileIdsMatchingTerm(string term)
        {
            return _suggestionRepository.GetBookFileIdsMatchingTerm(term);
        }

        public void Clear(List<int> bookFileIds)
        {
            _suggestionRepository.DeleteByBookFileIds(bookFileIds);
            _contributorEvidenceRepository.DeleteByBookFileIdsAndSources(bookFileIds, new[] { "aiReview", "sttTranscript" });
        }

        private ManualImportIdentificationSuggestionResource DeepIdentifyResource(BookFileResource resource)
        {
            if (!IsAudioFile(resource.Path))
            {
                return DisabledSuggestion("deepAudio", resource.Path, "File extension is not recognized as audio.");
            }

            var transcription = _audioIntroTranscriptionService.Transcribe(resource);

            return new ManualImportIdentificationSuggestionResource
            {
                Type = "deepAudio",
                Provider = transcription.Provider,
                Status = transcription.Status,
                Path = resource.Path,
                LikelyAuthor = transcription.Clues?.Author,
                LikelyBook = transcription.Clues?.Title,
                Narrator = transcription.Clues?.Narrator,
                Confidence = transcription.Clues?.Confidence > 0 ? transcription.Clues.Confidence : null,
                RequiresManualConfirmation = true,
                Explanation = transcription.Explanation,
                Transcript = transcription.Transcript,
                TranscriptIsTruncated = transcription.TranscriptIsTruncated,
                TranscriptExcerpt = transcription.TranscriptExcerpt,
                ContextSummary = transcription.ContextSummary,
                Stage = transcription.Stage,
                ProviderEndpoint = transcription.ProviderEndpoint,
                ProviderModel = transcription.ProviderModel,
                ProviderStatusCode = transcription.ProviderStatusCode,
                ProviderDurationMs = transcription.ProviderDurationMs,
                ProviderResponseExcerpt = transcription.ProviderResponseExcerpt,
                Steps = (transcription.Steps ?? new List<AudioIntroTranscriptionStep>())
                    .Select(x => Step(x.Kind, x.Label, x.Detail))
                    .ToList(),
                AudioPreviewUrl = $"/bookFile/unmapped/{resource.Id}/intro-preview"
            };
        }

        private OpenRouterConfig BuildConfig(OpenRouterConfigTestResource resource = null)
        {
            var apiKey = resource?.OpenRouterApiKey;

            if (apiKey == DevelopmentConfigResourceMapper.RedactedSecret)
            {
                apiKey = _configService.OpenRouterApiKey;
            }

            return new OpenRouterConfig
            {
                Enabled = resource?.OpenRouterEnabled ?? _configService.OpenRouterEnabled,
                ApiKey = apiKey.IsNotNullOrWhiteSpace() ? apiKey : _configService.OpenRouterApiKey,
                BaseUrl = (resource?.OpenRouterBaseUrl).IsNotNullOrWhiteSpace() ? resource.OpenRouterBaseUrl.TrimEnd('/') : _configService.OpenRouterBaseUrl.TrimEnd('/'),
                Model = (resource?.OpenRouterModel).IsNotNullOrWhiteSpace() ? resource.OpenRouterModel : _configService.OpenRouterModel,
                Timeout = Math.Max(5, resource?.OpenRouterTimeout ?? _configService.OpenRouterTimeout),
                MaxFileContext = Math.Max(1, resource?.OpenRouterMaxFileContext ?? _configService.OpenRouterMaxFileContext)
            };
        }

        private HttpResponse SendChatCompletion(OpenRouterConfig config, string prompt, int maxTokens)
        {
            var request = new HttpRequest($"{config.BaseUrl}/chat/completions")
            {
                Method = global::System.Net.Http.HttpMethod.Post,
                RequestTimeout = TimeSpan.FromSeconds(config.Timeout),
                SuppressHttpError = true,
                LogResponseContent = false
            };

            request.Headers.Set("Authorization", $"Bearer {config.ApiKey}");
            request.Headers.Set("Content-Type", "application/json");
            request.SetContent(new
            {
                model = config.Model,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You help classify audiobook and ebook files for a user's private library. Return concise structured JSON only."
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                temperature = 0.1,
                max_tokens = maxTokens
            }.ToJson());
            request.ContentSummary = $"OpenRouter chat completion with {prompt.Length} characters of bounded triage context";

            return _httpClient.Post(request);
        }

        private static string BuildAiReviewPrompt(List<BookFileResource> resources)
        {
            var files = resources.Select(resource => new
            {
                resource.Id,
                resource.Path,
                Parsed = resource.Review?.Parsed,
                Candidate = resource.Review?.Candidate,
                Confidence = resource.Review?.Confidence,
                Reasons = resource.Review?.Reasons?.Select(x => new { x.Kind, x.Label, x.Detail }).ToList(),
                Hints = resource.Review?.Hints?.Select(x => new { x.Kind, x.Label, x.Detail }).ToList()
            }).ToList();

            return "Review these unmapped library files. Do not guess beyond the evidence. Return JSON with a suggestions array. Each suggestion must include path, likelyAuthor, likelyBook, likelyEdition, language, narrator, confidence, explanation, and requiresManualConfirmation. Context:\n" + files.ToJson();
        }

        private static string ExtractMessageContent(string responseContent)
        {
            var json = JObject.Parse(responseContent);
            return json["choices"]?.FirstOrDefault()?["message"]?["content"]?.ToString() ?? responseContent;
        }

        private static List<ManualImportIdentificationSuggestionResource> ParseSuggestions(string content, List<BookFileResource> resources)
        {
            var suggestions = new List<ManualImportIdentificationSuggestionResource>();
            var json = ExtractJsonObject(content);
            var root = JObject.Parse(json);
            var array = root["suggestions"] as JArray ?? new JArray(root);

            foreach (var token in array)
            {
                var path = token.Value<string>("path");

                suggestions.Add(new ManualImportIdentificationSuggestionResource
                {
                    Type = "aiReview",
                    Provider = "openrouter",
                    Status = "suggested",
                    Path = path,
                    LikelyAuthor = token.Value<string>("likelyAuthor"),
                    LikelyBook = token.Value<string>("likelyBook"),
                    LikelyEdition = token.Value<string>("likelyEdition"),
                    Language = token.Value<string>("language"),
                    Narrator = token.Value<string>("narrator"),
                    Confidence = token.Value<int?>("confidence"),
                    Explanation = token.Value<string>("explanation"),
                    RequiresManualConfirmation = token.Value<bool?>("requiresManualConfirmation") ?? true,
                    ContextSummary = "AI review used bounded file metadata, current parser candidate, confidence, and rejection reasons."
                });
            }

            return suggestions.Where(x => resources.Any(resource => PathEquals(resource.Path, x.Path))).ToList();
        }

        private static string ExtractJsonObject(string content)
        {
            var start = content.IndexOf('{');
            var end = content.LastIndexOf('}');

            if (start < 0 || end <= start)
            {
                throw new InvalidOperationException("AI response did not contain a JSON object.");
            }

            return content.Substring(start, end - start + 1);
        }

        private static bool IsAudioFile(string path)
        {
            return AudioExtensions.Contains(Path.GetExtension(path ?? string.Empty));
        }

        private static ManualImportIdentificationSuggestionResource DisabledSuggestion(string type, string path, string explanation)
        {
            return StatusSuggestion(type, path, "disabled", explanation);
        }

        private static ManualImportIdentificationSuggestionResource StatusSuggestion(string type, string path, string status, string explanation)
        {
            List<ManualImportReviewReasonResource> steps = null;

            if (type == "deepAudio")
            {
                steps = new List<ManualImportReviewReasonResource>
                {
                    Step(status, GetStatusLabel(status), explanation)
                };
            }

            return new ManualImportIdentificationSuggestionResource
            {
                Type = type,
                Provider = type == "aiReview" ? "openrouter" : "openrouter-stt-foundation",
                Status = status,
                Path = path,
                RequiresManualConfirmation = true,
                Explanation = explanation,
                Steps = steps
            };
        }

        private void Store(List<BookFileResource> resources, List<ManualImportIdentificationSuggestionResource> suggestions, bool includeDisabled = false)
        {
            var now = DateTime.UtcNow;
            var storableSuggestions = suggestions.Where(x => includeDisabled || x.Status != "disabled").ToList();
            var resourcesByPath = resources
                .Where(x => x.Path.IsNotNullOrWhiteSpace())
                .GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
            var bookFileIds = new List<int>();
            var sources = new List<string>();

            foreach (var suggestion in storableSuggestions)
            {
                if (suggestion.Path.IsNullOrWhiteSpace())
                {
                    continue;
                }

                if (!resourcesByPath.TryGetValue(suggestion.Path, out var resource))
                {
                    continue;
                }

                bookFileIds.Add(resource.Id);
                sources.Add(GetContributorEvidenceSource(suggestion));
            }

            _contributorEvidenceRepository.DeleteByBookFileIdsAndSources(bookFileIds, sources);

            foreach (var suggestion in storableSuggestions)
            {
                if (suggestion.Path.IsNullOrWhiteSpace())
                {
                    continue;
                }

                if (!resourcesByPath.TryGetValue(suggestion.Path, out var resource))
                {
                    continue;
                }

                _suggestionRepository.Insert(new UnmappedFileIdentificationSuggestion
                {
                    BookFileId = resource.Id,
                    Path = resource.Path,
                    Size = resource.Size,
                    Modified = resource.Modified,
                    Type = suggestion.Type,
                    Provider = suggestion.Provider,
                    Status = suggestion.Status,
                    LikelyAuthor = suggestion.LikelyAuthor,
                    LikelyBook = suggestion.LikelyBook,
                    LikelyEdition = suggestion.LikelyEdition,
                    Language = suggestion.Language,
                    Narrator = suggestion.Narrator,
                    Confidence = suggestion.Confidence,
                    Explanation = suggestion.Explanation,
                    RequiresManualConfirmation = suggestion.RequiresManualConfirmation,
                    Transcript = suggestion.Transcript,
                    TranscriptIsTruncated = suggestion.TranscriptIsTruncated,
                    TranscriptExcerpt = suggestion.TranscriptExcerpt,
                    ContextSummary = suggestion.ContextSummary,
                    Stage = suggestion.Stage,
                    ProviderEndpoint = suggestion.ProviderEndpoint,
                    ProviderModel = suggestion.ProviderModel,
                    ProviderStatusCode = suggestion.ProviderStatusCode,
                    ProviderDurationMs = suggestion.ProviderDurationMs,
                    ProviderResponseExcerpt = suggestion.ProviderResponseExcerpt,
                    StepLog = suggestion.Steps?.ToJson(),
                    Created = now,
                    Updated = now
                });

                var contributorEvidence = BuildContributorEvidence(resource, suggestion, now);

                if (contributorEvidence != null)
                {
                    _contributorEvidenceRepository.Insert(contributorEvidence);
                }
            }
        }

        private static ContributorEvidence BuildContributorEvidence(BookFileResource resource, ManualImportIdentificationSuggestionResource suggestion, DateTime now)
        {
            if (suggestion.Narrator.IsNullOrWhiteSpace())
            {
                return null;
            }

            return new ContributorEvidence
            {
                BookFileId = resource.Id,
                Role = "narrator",
                DisplayName = suggestion.Narrator.Trim(),
                NormalizedName = ContributorEvidence.NormalizeName(suggestion.Narrator),
                Source = GetContributorEvidenceSource(suggestion),
                Confidence = suggestion.Confidence,
                RawValue = suggestion.Narrator,
                Created = now,
                Updated = now
            };
        }

        private static string GetContributorEvidenceSource(ManualImportIdentificationSuggestionResource suggestion)
        {
            if (suggestion.Type == "deepAudio")
            {
                return "sttTranscript";
            }

            if (suggestion.Type == "aiReview")
            {
                return "aiReview";
            }

            return suggestion.Type;
        }

        private static ManualImportIdentificationSuggestionResource ToResource(UnmappedFileIdentificationSuggestion suggestion, BookFileResource resource)
        {
            var isStale = !PathEquals(suggestion.Path, resource.Path) ||
                          suggestion.Size != resource.Size ||
                          suggestion.Modified != resource.Modified;

            return new ManualImportIdentificationSuggestionResource
            {
                Type = suggestion.Type,
                Provider = suggestion.Provider,
                Status = suggestion.Status,
                Path = suggestion.Path,
                LikelyAuthor = suggestion.LikelyAuthor,
                LikelyBook = suggestion.LikelyBook,
                LikelyEdition = suggestion.LikelyEdition,
                Language = suggestion.Language,
                Narrator = suggestion.Narrator,
                Confidence = suggestion.Confidence,
                Explanation = suggestion.Explanation,
                RequiresManualConfirmation = suggestion.RequiresManualConfirmation,
                Transcript = suggestion.Transcript,
                TranscriptIsTruncated = suggestion.TranscriptIsTruncated,
                TranscriptExcerpt = suggestion.TranscriptExcerpt,
                ContextSummary = suggestion.ContextSummary,
                Stage = suggestion.Stage,
                ProviderEndpoint = suggestion.ProviderEndpoint,
                ProviderModel = suggestion.ProviderModel,
                ProviderStatusCode = suggestion.ProviderStatusCode,
                ProviderDurationMs = suggestion.ProviderDurationMs,
                ProviderResponseExcerpt = suggestion.ProviderResponseExcerpt,
                AudioPreviewUrl = suggestion.Type == "deepAudio" ? $"/bookFile/unmapped/{resource.Id}/intro-preview" : null,
                Steps = ParseSteps(suggestion.StepLog),
                IsStale = isStale,
                Created = suggestion.Created,
                Updated = suggestion.Updated
            };
        }

        private static OpenRouterConfigTestResource Failed(OpenRouterConfigTestResource resource, string message, string detail)
        {
            return new OpenRouterConfigTestResource
            {
                OpenRouterEnabled = resource.OpenRouterEnabled,
                OpenRouterBaseUrl = resource.OpenRouterBaseUrl,
                OpenRouterModel = resource.OpenRouterModel,
                OpenRouterTimeout = resource.OpenRouterTimeout,
                IsHealthy = false,
                Message = message,
                Detail = detail
            };
        }

        private static bool PathEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string Truncate(string value, int maxLength)
        {
            if (value == null || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength);
        }

        private static ManualImportReviewReasonResource Step(string kind, string label, string detail)
        {
            return new ManualImportReviewReasonResource
            {
                Kind = kind,
                Label = label,
                Detail = detail
            };
        }

        private static List<ManualImportReviewReasonResource> MergeQueuedSteps(List<ManualImportReviewReasonResource> steps)
        {
            var result = new List<ManualImportReviewReasonResource>
            {
                Step("queued", "Queued", "Deep Identify Audio was queued for background processing.")
            };

            if (steps != null)
            {
                result.AddRange(steps.Where(x => x.Kind != "queued"));
            }

            return result;
        }

        private static List<ManualImportReviewReasonResource> ParseSteps(string stepLog)
        {
            if (stepLog.IsNullOrWhiteSpace())
            {
                return new List<ManualImportReviewReasonResource>();
            }

            try
            {
                return JsonConvert.DeserializeObject<List<ManualImportReviewReasonResource>>(stepLog) ?? new List<ManualImportReviewReasonResource>();
            }
            catch
            {
                return new List<ManualImportReviewReasonResource>();
            }
        }

        private static string GetStatusLabel(string status)
        {
            switch (status)
            {
                case "queued":
                    return "Queued";
                case "skipped":
                    return "Skipped";
                case "extractingIntro":
                    return "Extracting intro clip";
                case "disabled":
                    return "Not configured";
                default:
                    return status;
            }
        }

        private sealed class OpenRouterConfig
        {
            public bool Enabled { get; set; }
            public string ApiKey { get; set; }
            public string BaseUrl { get; set; }
            public string Model { get; set; }
            public int Timeout { get; set; }
            public int MaxFileContext { get; set; }
        }
    }
}
