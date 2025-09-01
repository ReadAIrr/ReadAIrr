using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Instrumentation;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.GoogleBooks.Resources;

namespace NzbDrone.Core.MetadataSource.GoogleBooks
{
    public interface IGoogleBooksProxy
    {
        Task<AudioBookMetadata> GetAudioBookMetadata(string isbn);
        Task<AudioBookMetadata> GetAudioBookMetadata(string title, string author);
        Task<List<GoogleBooksSearchResult>> SearchBooks(string query);
        Task<bool> ValidateApiKey(string apiKey);
    }

    public class GoogleBooksProxy : IGoogleBooksProxy
    {
        private readonly IHttpClient _httpClient;
        private readonly IConfigService _configService;
        private readonly Logger _logger;
        private readonly string _baseUrl = "https://www.googleapis.com/books/v1";

        private static readonly JsonSerializerOptions SerializerSettings = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public GoogleBooksProxy(IHttpClient httpClient, IConfigService configService, Logger logger)
        {
            _httpClient = httpClient;
            _configService = configService;
            _logger = logger;
        }

        public async Task<AudioBookMetadata> GetAudioBookMetadata(string isbn)
        {
            var apiKey = _configService.GoogleBooksApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.Debug("Google Books API key not configured");
                return null;
            }

            try
            {
                var url = $"{_baseUrl}/volumes?q=isbn:{isbn}&key={apiKey}";
                var request = new HttpRequest(url);
                request.SuppressHttpError = true;

                var response = await _httpClient.GetAsync(request);
                
                if (response.HasHttpError)
                {
                    _logger.Warn("Google Books API request failed with status: {0}", response.StatusCode);
                    return null;
                }

                var searchResponse = JsonSerializer.Deserialize<GoogleBooksSearchResponse>(response.Content, SerializerSettings);
                var audiobook = FindAudiobookInResults(searchResponse);
                
                return audiobook != null ? ExtractAudioBookMetadata(audiobook) : null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error fetching audiobook metadata from Google Books for ISBN: {0}", isbn);
                return null;
            }
        }

        public async Task<AudioBookMetadata> GetAudioBookMetadata(string title, string author)
        {
            var apiKey = _configService.GoogleBooksApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.Debug("Google Books API key not configured");
                return null;
            }

            try
            {
                var query = $"intitle:{title}";
                if (!string.IsNullOrEmpty(author))
                {
                    query += $"+inauthor:{author}";
                }

                var url = $"{_baseUrl}/volumes?q={Uri.EscapeDataString(query)}&key={apiKey}";
                var request = new HttpRequest(url);
                request.SuppressHttpError = true;

                var response = await _httpClient.GetAsync(request);
                
                if (response.HasHttpError)
                {
                    _logger.Warn("Google Books API request failed with status: {0}", response.StatusCode);
                    return null;
                }

                var searchResponse = JsonSerializer.Deserialize<GoogleBooksSearchResponse>(response.Content, SerializerSettings);
                var audiobook = FindAudiobookInResults(searchResponse);
                
                return audiobook != null ? ExtractAudioBookMetadata(audiobook) : null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error fetching audiobook metadata from Google Books for: {0} by {1}", title, author);
                return null;
            }
        }

        public async Task<List<GoogleBooksSearchResult>> SearchBooks(string query)
        {
            var apiKey = _configService.GoogleBooksApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                return new List<GoogleBooksSearchResult>();
            }

            try
            {
                var url = $"{_baseUrl}/volumes?q={Uri.EscapeDataString(query)}&key={apiKey}&maxResults=20";
                var request = new HttpRequest(url);
                request.SuppressHttpError = true;

                var response = await _httpClient.GetAsync(request);
                
                if (response.HasHttpError)
                {
                    return new List<GoogleBooksSearchResult>();
                }

                var searchResponse = JsonSerializer.Deserialize<GoogleBooksSearchResponse>(response.Content, SerializerSettings);
                return searchResponse?.Items?.Select(MapSearchResult).ToList() ?? new List<GoogleBooksSearchResult>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error searching Google Books for: {0}", query);
                return new List<GoogleBooksSearchResult>();
            }
        }

        public async Task<bool> ValidateApiKey(string apiKey)
        {
            try
            {
                var url = $"{_baseUrl}/volumes?q=test&key={apiKey}&maxResults=1";
                var request = new HttpRequest(url);
                request.SuppressHttpError = true;

                var response = await _httpClient.GetAsync(request);
                
                // Check for successful response or valid error codes (not auth failures)
                return response.StatusCode == HttpStatusCode.OK || 
                       (response.HasHttpError && 
                        response.StatusCode != HttpStatusCode.Forbidden && 
                        response.StatusCode != HttpStatusCode.Unauthorized);
            }
            catch
            {
                return false;
            }
        }

        private GoogleBooksVolumeInfo FindAudiobookInResults(GoogleBooksSearchResponse response)
        {
            if (response?.Items == null || !response.Items.Any())
            {
                return null;
            }

            // Look for audiobook format first
            var audiobook = response.Items
                .Where(item => IsAudiobook(item.VolumeInfo))
                .FirstOrDefault();

            return audiobook?.VolumeInfo;
        }

        private bool IsAudiobook(GoogleBooksVolumeInfo volumeInfo)
        {
            if (volumeInfo == null) return false;

            // Check if explicitly marked as audiobook
            if (volumeInfo.Categories?.Any(c => c.Contains("Audiobook", StringComparison.OrdinalIgnoreCase)) == true)
                return true;

            // Check format information
            if (volumeInfo.Description?.Contains("audiobook", StringComparison.OrdinalIgnoreCase) == true)
                return true;

            // Check if it has duration information (likely indicates audiobook)
            if (!string.IsNullOrEmpty(volumeInfo.Duration))
                return true;

            return false;
        }

        private AudioBookMetadata ExtractAudioBookMetadata(GoogleBooksVolumeInfo volumeInfo)
        {
            if (volumeInfo == null)
                return null;

            return new AudioBookMetadata
            {
                Title = volumeInfo.Title,
                Authors = volumeInfo.Authors?.ToList() ?? new List<string>(),
                Narrators = ExtractNarrators(volumeInfo),
                Duration = ParseDuration(volumeInfo.Duration),
                Language = volumeInfo.Language,
                Publisher = volumeInfo.Publisher,
                PublishedDate = TryParseDate(volumeInfo.PublishedDate),
                Description = volumeInfo.Description,
                PageCount = volumeInfo.PageCount,
                Categories = volumeInfo.Categories?.ToList() ?? new List<string>(),
                AverageRating = volumeInfo.AverageRating,
                RatingsCount = volumeInfo.RatingsCount ?? 0,
                MaturityRating = volumeInfo.MaturityRating,
                ImageUrl = GetBestImageUrl(volumeInfo.ImageLinks),
                PreviewLink = volumeInfo.PreviewLink,
                InfoLink = volumeInfo.InfoLink,
                CanonicalVolumeLink = volumeInfo.CanonicalVolumeLink,
                IndustryIdentifiers = volumeInfo.IndustryIdentifiers?.ToList() ?? new List<GoogleBooksIndustryIdentifier>()
            };
        }

        private List<string> ExtractNarrators(GoogleBooksVolumeInfo volumeInfo)
        {
            var narrators = new List<string>();

            // Try to extract narrator info from description
            if (!string.IsNullOrEmpty(volumeInfo.Description))
            {
                var description = volumeInfo.Description.ToLowerInvariant();
                
                // Look for common narrator patterns
                var narratorPatterns = new[]
                {
                    @"narrated by ([^.]+)",
                    @"narrator: ([^.]+)",
                    @"read by ([^.]+)",
                    @"performed by ([^.]+)"
                };

                foreach (var pattern in narratorPatterns)
                {
                    var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    var match = regex.Match(description);
                    if (match.Success)
                    {
                        narrators.Add(match.Groups[1].Value.Trim());
                        break; // Take the first match
                    }
                }
            }

            return narrators;
        }

        private TimeSpan? ParseDuration(string duration)
        {
            if (string.IsNullOrEmpty(duration))
                return null;

            // Try to parse various duration formats
            // Examples: "8 hours 32 minutes", "8h 32m", "512 minutes", etc.
            
            var lowerDuration = duration.ToLowerInvariant();
            int totalMinutes = 0;

            // Extract hours
            var hourMatch = System.Text.RegularExpressions.Regex.Match(lowerDuration, @"(\d+)\s*(?:hours?|hrs?|h)");
            if (hourMatch.Success)
            {
                totalMinutes += int.Parse(hourMatch.Groups[1].Value) * 60;
            }

            // Extract minutes
            var minuteMatch = System.Text.RegularExpressions.Regex.Match(lowerDuration, @"(\d+)\s*(?:minutes?|mins?|m)");
            if (minuteMatch.Success)
            {
                totalMinutes += int.Parse(minuteMatch.Groups[1].Value);
            }

            // If only minutes mentioned (no hours)
            if (totalMinutes == 0 && System.Text.RegularExpressions.Regex.IsMatch(lowerDuration, @"^\d+\s*(?:minutes?|mins?)"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(lowerDuration, @"(\d+)");
                if (match.Success)
                {
                    totalMinutes = int.Parse(match.Groups[1].Value);
                }
            }

            return totalMinutes > 0 ? TimeSpan.FromMinutes(totalMinutes) : null;
        }

        private DateTime? TryParseDate(string dateString)
        {
            if (string.IsNullOrEmpty(dateString))
                return null;

            return DateTime.TryParse(dateString, out var result) ? result : null;
        }

        private string GetBestImageUrl(GoogleBooksImageLinks imageLinks)
        {
            if (imageLinks == null)
                return null;

            // Prefer larger images
            return imageLinks.ExtraLarge ??
                   imageLinks.Large ??
                   imageLinks.Medium ??
                   imageLinks.Small ??
                   imageLinks.Thumbnail ??
                   imageLinks.SmallThumbnail;
        }

        private GoogleBooksSearchResult MapSearchResult(GoogleBooksVolume volume)
        {
            return new GoogleBooksSearchResult
            {
                Id = volume.Id,
                Title = volume.VolumeInfo?.Title,
                Authors = volume.VolumeInfo?.Authors?.ToList(),
                IsAudiobook = IsAudiobook(volume.VolumeInfo),
                Duration = ParseDuration(volume.VolumeInfo?.Duration),
                ImageUrl = GetBestImageUrl(volume.VolumeInfo?.ImageLinks),
                Description = volume.VolumeInfo?.Description
            };
        }
    }

    public class AudioBookMetadata
    {
        public string Title { get; set; }
        public List<string> Authors { get; set; } = new List<string>();
        public List<string> Narrators { get; set; } = new List<string>();
        public TimeSpan? Duration { get; set; }
        public string Language { get; set; }
        public string Publisher { get; set; }
        public DateTime? PublishedDate { get; set; }
        public string Description { get; set; }
        public int? PageCount { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
        public double? AverageRating { get; set; }
        public int RatingsCount { get; set; }
        public string MaturityRating { get; set; }
        public string ImageUrl { get; set; }
        public string PreviewLink { get; set; }
        public string InfoLink { get; set; }
        public string CanonicalVolumeLink { get; set; }
        public List<GoogleBooksIndustryIdentifier> IndustryIdentifiers { get; set; } = new List<GoogleBooksIndustryIdentifier>();
    }

    public class GoogleBooksSearchResult
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public List<string> Authors { get; set; }
        public bool IsAudiobook { get; set; }
        public TimeSpan? Duration { get; set; }
        public string ImageUrl { get; set; }
        public string Description { get; set; }
    }
}
