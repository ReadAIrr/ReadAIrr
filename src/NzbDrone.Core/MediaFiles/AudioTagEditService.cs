using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Exceptions;

namespace NzbDrone.Core.MediaFiles
{
    public class AudioTagValues
    {
        public string Title { get; set; }
        public string Book { get; set; }
        public string Author { get; set; }
        public string Performers { get; set; }
        public int Track { get; set; }
        public int TrackCount { get; set; }
        public int Disc { get; set; }
        public int DiscCount { get; set; }
        public string Date { get; set; }
        public int Year { get; set; }
        public string Publisher { get; set; }
        public string Genres { get; set; }
        public string Comment { get; set; }
    }

    public class AudioTagEditDifference
    {
        public string Field { get; set; }
        public string CurrentValue { get; set; }
        public string ProposedValue { get; set; }
    }

    public class AudioTagEditPreview
    {
        public int BookFileId { get; set; }
        public string Path { get; set; }
        public bool IsAudioFile { get; set; }
        public bool CanWrite { get; set; }
        public string Warning { get; set; }
        public AudioTagValues Current { get; set; }
        public AudioTagValues Suggested { get; set; }
        public AudioTagValues Proposed { get; set; }
        public List<AudioTagEditDifference> Changes { get; set; }
    }

    public interface IAudioTagEditService
    {
        AudioTagEditPreview GetPreview(int bookFileId);
        AudioTagEditPreview Preview(int bookFileId, AudioTagValues proposed);
        AudioTagEditPreview Write(int bookFileId, AudioTagValues proposed);
    }

    public class AudioTagEditService : IAudioTagEditService
    {
        private readonly IMediaFileService _mediaFileService;
        private readonly IAudioTagService _audioTagService;
        private readonly IContributorEvidenceRepository _contributorEvidenceRepository;
        private readonly IMultiFileBookFileCompletenessService _multiFileCompletenessService;
        private readonly Logger _logger;

        public AudioTagEditService(IMediaFileService mediaFileService,
                                   IAudioTagService audioTagService,
                                   IContributorEvidenceRepository contributorEvidenceRepository,
                                   IMultiFileBookFileCompletenessService multiFileCompletenessService,
                                   Logger logger)
        {
            _mediaFileService = mediaFileService;
            _audioTagService = audioTagService;
            _contributorEvidenceRepository = contributorEvidenceRepository;
            _multiFileCompletenessService = multiFileCompletenessService;
            _logger = logger;
        }

        public AudioTagEditPreview GetPreview(int bookFileId)
        {
            var bookFile = GetAudioBookFile(bookFileId);
            var current = _audioTagService.ReadAudioTag(bookFile.Path);
            var suggested = GetSuggestedTags(bookFile, current, out var warning);

            return BuildPreview(bookFile, current, suggested, suggested, warning);
        }

        public AudioTagEditPreview Preview(int bookFileId, AudioTagValues proposed)
        {
            var bookFile = GetAudioBookFile(bookFileId);
            var current = _audioTagService.ReadAudioTag(bookFile.Path);
            var suggested = GetSuggestedTags(bookFile, current, out var warning);
            var proposedTag = ToAudioTag(proposed, current);

            return BuildPreview(bookFile, current, suggested, proposedTag, warning);
        }

        public AudioTagEditPreview Write(int bookFileId, AudioTagValues proposed)
        {
            var bookFile = GetAudioBookFile(bookFileId);
            var current = _audioTagService.ReadAudioTag(bookFile.Path);
            var suggested = GetSuggestedTags(bookFile, current, out var warning);
            var proposedTag = ToAudioTag(proposed, current);
            var preview = BuildPreview(bookFile, current, suggested, proposedTag, warning);

            if (!preview.Changes.Any())
            {
                return preview;
            }

            _logger.ProgressInfo("Writing manual audio tags for {0}", bookFile.Path);
            _audioTagService.WriteManualTags(bookFile, proposedTag);

            var updatedCurrent = _audioTagService.ReadAudioTag(bookFile.Path);
            return BuildPreview(bookFile, updatedCurrent, suggested, updatedCurrent, warning);
        }

        private BookFile GetAudioBookFile(int bookFileId)
        {
            var bookFile = _mediaFileService.Get(bookFileId);

            if (bookFile == null)
            {
                throw new NzbDroneClientException(System.Net.HttpStatusCode.NotFound, "Book file not found");
            }

            if (!MediaFileExtensions.AudioExtensions.Contains(Path.GetExtension(bookFile.Path)))
            {
                throw new BadRequestException("Manual audio tag editing is only available for audio files");
            }

            if (bookFile.EditionId <= 0 || bookFile.Edition.Value == null)
            {
                throw new BadRequestException("Manual audio tag editing is only available for imported files matched to an edition");
            }

            return bookFile;
        }

        private AudioTag GetSuggestedTags(BookFile bookFile, AudioTag current, out string warning)
        {
            warning = null;
            var suggested = _audioTagService.GetTrackMetadata(bookFile);
            var narrator = GetTrustedNarratorEvidence(bookFile);

            if (narrator.IsNotNullOrWhiteSpace())
            {
                suggested.Performers = new[] { narrator };
            }

            var issue = _multiFileCompletenessService.GetIssue(bookFile.Edition.Value.BookFiles.Value);

            if (issue != null)
            {
                warning = $"This file belongs to an incomplete audiobook part set. Automated retagging is blocked: {issue.Message}. Manual edits affect only this file and will not normalize track count.";
                suggested.TrackCount = current.TrackCount;
            }

            return suggested;
        }

        private string GetTrustedNarratorEvidence(BookFile bookFile)
        {
            var evidence = _contributorEvidenceRepository.GetByBookFileIds(new[] { bookFile.Id })
                .Concat(_contributorEvidenceRepository.GetByEditionIds(new[] { bookFile.EditionId }))
                .Where(x => x.Role == "narrator" && x.DisplayName.IsNotNullOrWhiteSpace())
                .Where(x => IsManualEvidence(x) || IsHighConfidenceReviewEvidence(x))
                .OrderByDescending(x => IsManualEvidence(x))
                .ThenByDescending(x => x.Confidence ?? 0)
                .ThenByDescending(x => x.Updated)
                .FirstOrDefault();

            return evidence?.DisplayName;
        }

        private static bool IsManualEvidence(ContributorEvidence evidence)
        {
            return evidence.Source == "manual";
        }

        private static bool IsHighConfidenceReviewEvidence(ContributorEvidence evidence)
        {
            return (evidence.Source == "aiReview" || evidence.Source == "sttTranscript") && (evidence.Confidence ?? 0) >= 80;
        }

        private AudioTagEditPreview BuildPreview(BookFile bookFile, AudioTag current, AudioTag suggested, AudioTag proposed, string warning)
        {
            return new AudioTagEditPreview
            {
                BookFileId = bookFile.Id,
                Path = bookFile.Path,
                IsAudioFile = true,
                CanWrite = current.IsValid && proposed.IsValid,
                Warning = warning,
                Current = ToValues(current),
                Suggested = ToValues(suggested),
                Proposed = ToValues(proposed),
                Changes = current.Diff(proposed).Select(x => new AudioTagEditDifference
                {
                    Field = x.Key,
                    CurrentValue = x.Value.Item1,
                    ProposedValue = x.Value.Item2
                }).ToList()
            };
        }

        private static AudioTagValues ToValues(AudioTag tag)
        {
            return new AudioTagValues
            {
                Title = tag.Title,
                Book = tag.Book,
                Author = JoinValues(tag.BookAuthors),
                Performers = JoinValues(tag.Performers),
                Track = (int)tag.Track,
                TrackCount = (int)tag.TrackCount,
                Disc = (int)tag.Disc,
                DiscCount = (int)tag.DiscCount,
                Date = tag.Date.HasValue ? tag.Date.Value.ToString("yyyy-MM-dd") : null,
                Year = (int)tag.Year,
                Publisher = tag.Publisher,
                Genres = JoinValues(tag.Genres),
                Comment = tag.Comment
            };
        }

        private static AudioTag ToAudioTag(AudioTagValues values, AudioTag current)
        {
            values = values ?? new AudioTagValues();

            var date = ParseDate(values.Date, values.Year);
            var year = values.Year > 0 ? values.Year : date?.Year ?? 0;

            return new AudioTag
            {
                Title = values.Title,
                Book = values.Book,
                BookAuthors = SplitValues(values.Author),
                Performers = SplitValues(values.Performers),
                Track = ToUInt(values.Track),
                TrackCount = ToUInt(values.TrackCount),
                Disc = ToUInt(values.Disc),
                DiscCount = ToUInt(values.DiscCount),
                Date = date,
                Year = ToUInt(year),
                OriginalReleaseDate = current.OriginalReleaseDate,
                OriginalYear = current.OriginalYear,
                Publisher = values.Publisher,
                Genres = SplitValues(values.Genres),
                Comment = values.Comment,
                Media = current.Media,
                Duration = current.Duration,
                ImageSize = current.ImageSize,
                Quality = current.Quality,
                MediaInfo = current.MediaInfo
            };
        }

        private static DateTime? ParseDate(string date, int year)
        {
            if (date.IsNotNullOrWhiteSpace() &&
                DateTime.TryParseExact(date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            {
                return result;
            }

            return year > 0 ? new DateTime(year, 1, 1) : default(DateTime?);
        }

        private static uint ToUInt(int value)
        {
            return value > 0 ? (uint)value : 0;
        }

        private static string JoinValues(string[] values)
        {
            return values == null || !values.Any() ? null : string.Join("; ", values.Where(x => x.IsNotNullOrWhiteSpace()));
        }

        private static string[] SplitValues(string values)
        {
            return values.IsNullOrWhiteSpace() ?
                new string[0] :
                values.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.IsNotNullOrWhiteSpace()).ToArray();
        }
    }
}
