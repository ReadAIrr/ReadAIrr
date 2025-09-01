using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MetadataSource.GoogleBooks;

namespace NzbDrone.Core.Books.Services
{
    public interface IAudiobookDurationValidationService
    {
        Task<AudiobookValidationResult> ValidateAudiobookCompleteness(Edition edition);
        Task<List<AudiobookValidationResult>> ValidateMultipleAudiobooks(List<Edition> editions);
        Task<AudiobookHealthCheckResult> PerformHealthCheck(Author author);
    }

    public class AudiobookDurationValidationService : IAudiobookDurationValidationService
    {
        private readonly IMediaFileService _mediaFileService;
        private readonly IGoogleBooksProxy _googleBooksProxy;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public AudiobookDurationValidationService(
            IMediaFileService mediaFileService,
            IGoogleBooksProxy googleBooksProxy,
            IConfigService configService,
            Logger logger)
        {
            _mediaFileService = mediaFileService;
            _googleBooksProxy = googleBooksProxy;
            _configService = configService;
            _logger = logger;
        }

        public async Task<AudiobookValidationResult> ValidateAudiobookCompleteness(Edition edition)
        {
            var result = new AudiobookValidationResult
            {
                Edition = edition,
                IsValid = false,
                ValidationDate = DateTime.UtcNow
            };

            try
            {
                // Skip validation if Google Books is not configured
                if (!_configService.EnableGoogleBooksMetadata || string.IsNullOrEmpty(_configService.GoogleBooksApiKey))
                {
                    result.Status = ValidationStatus.Skipped;
                    result.Message = "Google Books API not configured";
                    return result;
                }

                // Get local media files for this edition
                var mediaFiles = _mediaFileService.GetFilesByEdition(edition.Id);
                if (!mediaFiles.Any())
                {
                    result.Status = ValidationStatus.NoMediaFiles;
                    result.Message = "No media files found for this edition";
                    return result;
                }

                // Calculate total duration from local files
                var localDuration = CalculateLocalDuration(mediaFiles);
                result.LocalDuration = localDuration;

                // Get expected duration from Google Books
                var expectedDuration = await GetExpectedDurationFromGoogleBooks(edition);
                if (!expectedDuration.HasValue)
                {
                    result.Status = ValidationStatus.NoExpectedDuration;
                    result.Message = "Unable to retrieve expected duration from Google Books";
                    return result;
                }

                result.ExpectedDuration = expectedDuration.Value;

                // Compare durations with tolerance
                var validationResult = CompareDurations(localDuration, expectedDuration.Value);
                result.IsValid = validationResult.IsValid;
                result.Status = validationResult.Status;
                result.Message = validationResult.Message;
                result.DurationDifference = validationResult.DurationDifference;
                result.DifferencePercentage = validationResult.DifferencePercentage;

                _logger.Debug("Duration validation for {Title}: Local={LocalDuration}, Expected={ExpectedDuration}, Valid={IsValid}",
                    edition.Title, localDuration, expectedDuration.Value, result.IsValid);

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error validating audiobook duration for edition {EditionId}", edition.Id);
                result.Status = ValidationStatus.Error;
                result.Message = $"Validation error: {ex.Message}";
                return result;
            }
        }

        public async Task<List<AudiobookValidationResult>> ValidateMultipleAudiobooks(List<Edition> editions)
        {
            var results = new List<AudiobookValidationResult>();

            foreach (var edition in editions)
            {
                try
                {
                    var result = await ValidateAudiobookCompleteness(edition);
                    results.Add(result);
                    
                    // Add small delay to respect API rate limits
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error validating edition {EditionId}", edition.Id);
                    results.Add(new AudiobookValidationResult
                    {
                        Edition = edition,
                        IsValid = false,
                        Status = ValidationStatus.Error,
                        Message = $"Validation error: {ex.Message}",
                        ValidationDate = DateTime.UtcNow
                    });
                }
            }

            return results;
        }

        public async Task<AudiobookHealthCheckResult> PerformHealthCheck(Author author)
        {
            var healthCheck = new AudiobookHealthCheckResult
            {
                Author = author,
                CheckDate = DateTime.UtcNow
            };

            try
            {
                var editions = author.Books.SelectMany(b => b.Editions).ToList();
                var validationResults = await ValidateMultipleAudiobooks(editions);
                
                healthCheck.TotalEditions = validationResults.Count;
                healthCheck.ValidEditions = validationResults.Count(r => r.IsValid);
                healthCheck.InvalidEditions = validationResults.Count(r => !r.IsValid && r.Status != ValidationStatus.Skipped);
                healthCheck.SkippedEditions = validationResults.Count(r => r.Status == ValidationStatus.Skipped);
                
                healthCheck.ValidationResults = validationResults;
                healthCheck.OverallHealth = CalculateOverallHealth(validationResults);
                
                return healthCheck;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error performing audiobook health check for author {AuthorId}", author.Id);
                healthCheck.Error = ex.Message;
                return healthCheck;
            }
        }

        private TimeSpan CalculateLocalDuration(List<BookFile> mediaFiles)
        {
            var totalDuration = TimeSpan.Zero;
            
            foreach (var file in mediaFiles)
            {
                if (file.AudioTags?.Duration != null)
                {
                    totalDuration = totalDuration.Add(file.AudioTags.Duration.Value);
                }
            }
            
            return totalDuration;
        }

        private async Task<TimeSpan?> GetExpectedDurationFromGoogleBooks(Edition edition)
        {
            try
            {
                // Try with ISBN first
                if (!string.IsNullOrEmpty(edition.Isbn13))
                {
                    var metadata = await _googleBooksProxy.GetAudioBookMetadata(edition.Isbn13);
                    if (metadata?.Duration.HasValue == true)
                    {
                        return metadata.Duration.Value;
                    }
                }

                // Try with ISBN10 if available
                if (!string.IsNullOrEmpty(edition.Isbn10))
                {
                    var metadata = await _googleBooksProxy.GetAudioBookMetadata(edition.Isbn10);
                    if (metadata?.Duration.HasValue == true)
                    {
                        return metadata.Duration.Value;
                    }
                }

                // Try with ASIN if available
                if (!string.IsNullOrEmpty(edition.Asin))
                {
                    var metadata = await _googleBooksProxy.GetAudioBookMetadata(edition.Asin);
                    if (metadata?.Duration.HasValue == true)
                    {
                        return metadata.Duration.Value;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Error getting expected duration from Google Books for edition {EditionId}", edition.Id);
                return null;
            }
        }

        private DurationComparisonResult CompareDurations(TimeSpan localDuration, TimeSpan expectedDuration)
        {
            var difference = localDuration - expectedDuration;
            var absDifference = difference.Duration();
            var expectedTotalMinutes = expectedDuration.TotalMinutes;
            var percentage = expectedTotalMinutes > 0 ? (absDifference.TotalMinutes / expectedTotalMinutes) * 100 : 0;

            // Define tolerance thresholds
            const double tolerancePercentage = 5.0; // 5% tolerance
            var toleranceMinutes = Math.Max(5, expectedTotalMinutes * 0.05); // At least 5 minutes or 5% of total

            if (absDifference.TotalMinutes <= toleranceMinutes && percentage <= tolerancePercentage)
            {
                return new DurationComparisonResult
                {
                    IsValid = true,
                    Status = ValidationStatus.Valid,
                    Message = "Duration matches expected value within tolerance",
                    DurationDifference = difference,
                    DifferencePercentage = percentage
                };
            }

            var status = localDuration < expectedDuration ? ValidationStatus.TooShort : ValidationStatus.TooLong;
            var direction = localDuration < expectedDuration ? "shorter" : "longer";
            
            return new DurationComparisonResult
            {
                IsValid = false,
                Status = status,
                Message = $"Duration is {direction} than expected by {FormatDuration(absDifference)} ({percentage:F1}%)",
                DurationDifference = difference,
                DifferencePercentage = percentage
            };
        }

        private string FormatDuration(TimeSpan duration)
        {
            return duration.ToString(@"h\:mm\:ss");
        }

        private HealthStatus CalculateOverallHealth(List<AudiobookValidationResult> results)
        {
            if (!results.Any())
                return HealthStatus.Unknown;

            var validCount = results.Count(r => r.IsValid);
            var totalCount = results.Count(r => r.Status != ValidationStatus.Skipped);
            
            if (totalCount == 0)
                return HealthStatus.Unknown;

            var healthPercentage = (double)validCount / totalCount * 100;

            if (healthPercentage >= 95)
                return HealthStatus.Excellent;
            if (healthPercentage >= 85)
                return HealthStatus.Good;
            if (healthPercentage >= 70)
                return HealthStatus.Fair;
            
            return HealthStatus.Poor;
        }
    }

    public class AudiobookValidationResult
    {
        public Edition Edition { get; set; }
        public bool IsValid { get; set; }
        public ValidationStatus Status { get; set; }
        public string Message { get; set; }
        public TimeSpan? LocalDuration { get; set; }
        public TimeSpan? ExpectedDuration { get; set; }
        public TimeSpan? DurationDifference { get; set; }
        public double? DifferencePercentage { get; set; }
        public DateTime ValidationDate { get; set; }
    }

    public class AudiobookHealthCheckResult
    {
        public Author Author { get; set; }
        public DateTime CheckDate { get; set; }
        public int TotalEditions { get; set; }
        public int ValidEditions { get; set; }
        public int InvalidEditions { get; set; }
        public int SkippedEditions { get; set; }
        public HealthStatus OverallHealth { get; set; }
        public List<AudiobookValidationResult> ValidationResults { get; set; } = new List<AudiobookValidationResult>();
        public string Error { get; set; }
    }

    public class DurationComparisonResult
    {
        public bool IsValid { get; set; }
        public ValidationStatus Status { get; set; }
        public string Message { get; set; }
        public TimeSpan DurationDifference { get; set; }
        public double DifferencePercentage { get; set; }
    }

    public enum ValidationStatus
    {
        Valid,
        TooShort,
        TooLong,
        NoMediaFiles,
        NoExpectedDuration,
        Skipped,
        Error
    }

    public enum HealthStatus
    {
        Unknown,
        Poor,
        Fair,
        Good,
        Excellent
    }
}
