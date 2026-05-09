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
        public List<string> EvidenceWarnings { get; set; }
        public List<string> WriteWarnings { get; set; }
        public AudioTagValues Current { get; set; }
        public AudioTagValues Suggested { get; set; }
        public AudioTagValues Proposed { get; set; }
        public List<AudioTagEditDifference> Changes { get; set; }
    }

    public class AudioTagTemplateOption
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
    }

    public class AudioTagTemplateRequest
    {
        public int? BookId { get; set; }
        public List<int> BookFileIds { get; set; }
        public string Template { get; set; }
    }

    public class AudioTagTemplatePreview
    {
        public string Template { get; set; }
        public List<AudioTagTemplateOption> Templates { get; set; }
        public string Warning { get; set; }
        public int TotalFiles { get; set; }
        public int WritableFiles { get; set; }
        public int ChangedFiles { get; set; }
        public int WarningFiles { get; set; }
        public List<AudioTagEditPreview> Files { get; set; }
    }

    public interface IAudioTagEditService
    {
        AudioTagEditPreview GetPreview(int bookFileId);
        AudioTagEditPreview Preview(int bookFileId, AudioTagValues proposed);
        AudioTagEditPreview Write(int bookFileId, AudioTagValues proposed);
        List<AudioTagTemplateOption> GetTemplates();
        AudioTagTemplatePreview PreviewTemplate(AudioTagTemplateRequest request);
        AudioTagTemplatePreview WriteTemplate(AudioTagTemplateRequest request);
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
            var writeWarnings = GetWriteWarnings(updatedCurrent, proposedTag);
            LogWriteWarnings(bookFile, writeWarnings);

            return BuildPreview(bookFile, updatedCurrent, suggested, proposedTag, warning, writeWarnings);
        }

        public List<AudioTagTemplateOption> GetTemplates()
        {
            return new List<AudioTagTemplateOption>
            {
                new AudioTagTemplateOption
                {
                    Name = "readarr",
                    Label = "ReadAIrr metadata",
                    Description = "Use ReadAIrr's matched edition, author, narrator evidence, publisher, release date, and current file order."
                },
                new AudioTagTemplateOption
                {
                    Name = "plexAudiobook",
                    Label = "Plex audiobook",
                    Description = "Use book title as album, author as album artist, narrator as performer, Audiobook genre fallback, and preserved safe file order."
                },
                new AudioTagTemplateOption
                {
                    Name = "minimal",
                    Label = "Minimal audiobook",
                    Description = "Write only title, book, author, narrator, and safe part numbering; leave publisher, genres, comment, and disc fields from current tags."
                }
            };
        }

        public AudioTagTemplatePreview PreviewTemplate(AudioTagTemplateRequest request)
        {
            return BuildTemplatePreview(request, false);
        }

        public AudioTagTemplatePreview WriteTemplate(AudioTagTemplateRequest request)
        {
            return BuildTemplatePreview(request, true);
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
            var narrator = GetTrustedNarratorEvidence(bookFile, out var evidenceWarning);

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

            if (evidenceWarning.IsNotNullOrWhiteSpace())
            {
                warning = warning.IsNullOrWhiteSpace() ? evidenceWarning : $"{warning} {evidenceWarning}";
            }

            return suggested;
        }

        private AudioTagTemplatePreview BuildTemplatePreview(AudioTagTemplateRequest request, bool write)
        {
            request = request ?? new AudioTagTemplateRequest();
            var template = NormalizeTemplate(request.Template);
            var bookFiles = GetAudioBookFiles(request);
            var files = new List<AudioTagEditPreview>();

            foreach (var bookFile in bookFiles)
            {
                var current = _audioTagService.ReadAudioTag(bookFile.Path);
                var suggested = GetSuggestedTags(bookFile, current, out var warning);
                var proposed = ApplyTemplate(template, current, suggested);
                var preview = BuildPreview(bookFile, current, suggested, proposed, warning);

                if (write && preview.Changes.Any())
                {
                    _logger.ProgressInfo("Writing {0} audio tag template for {1}", template, bookFile.Path);
                    _audioTagService.WriteManualTags(bookFile, proposed);

                    var updatedCurrent = _audioTagService.ReadAudioTag(bookFile.Path);
                    var writeWarnings = GetWriteWarnings(updatedCurrent, proposed);
                    LogWriteWarnings(bookFile, writeWarnings);

                    preview = BuildPreview(bookFile, updatedCurrent, suggested, proposed, warning, writeWarnings);
                }

                files.Add(preview);
            }

            return new AudioTagTemplatePreview
            {
                Template = template,
                Templates = GetTemplates(),
                Warning = GetBulkWarning(files),
                TotalFiles = files.Count,
                WritableFiles = files.Count(x => x.CanWrite),
                ChangedFiles = files.Count(x => x.Changes.Any()),
                WarningFiles = files.Count(x => x.Warning.IsNotNullOrWhiteSpace() || (x.WriteWarnings != null && x.WriteWarnings.Any())),
                Files = files
            };
        }

        private List<BookFile> GetAudioBookFiles(AudioTagTemplateRequest request)
        {
            var bookFiles = new List<BookFile>();

            if (request.BookFileIds != null && request.BookFileIds.Any())
            {
                bookFiles.AddRange(_mediaFileService.Get(request.BookFileIds) ?? new List<BookFile>());
            }
            else if (request.BookId.HasValue)
            {
                bookFiles.AddRange(_mediaFileService.GetFilesByBook(request.BookId.Value) ?? new List<BookFile>());
            }
            else
            {
                throw new BadRequestException("bookId or bookFileIds must be provided");
            }

            return bookFiles
                .Where(x => x != null)
                .Select(x => GetAudioBookFile(x.Id))
                .OrderBy(x => x.Part)
                .ThenBy(x => x.Path)
                .ToList();
        }

        private AudioTag ApplyTemplate(string template, AudioTag current, AudioTag suggested)
        {
            switch (template)
            {
                case "minimal":
                    return new AudioTag
                    {
                        Title = suggested.Title,
                        Book = suggested.Book,
                        BookAuthors = suggested.BookAuthors,
                        Performers = suggested.Performers,
                        Track = suggested.Track,
                        TrackCount = suggested.TrackCount,
                        Disc = current.Disc,
                        DiscCount = current.DiscCount,
                        Date = current.Date,
                        Year = current.Year,
                        OriginalReleaseDate = current.OriginalReleaseDate,
                        OriginalYear = current.OriginalYear,
                        Publisher = current.Publisher,
                        Genres = current.Genres,
                        Comment = current.Comment,
                        Media = current.Media,
                        Duration = current.Duration,
                        ImageSize = current.ImageSize,
                        Quality = current.Quality,
                        MediaInfo = current.MediaInfo
                    };

                case "plexAudiobook":
                    return new AudioTag
                    {
                        Title = suggested.Title,
                        Book = suggested.Book,
                        BookAuthors = suggested.BookAuthors,
                        Performers = suggested.Performers,
                        Track = suggested.Track,
                        TrackCount = suggested.TrackCount,
                        Disc = suggested.Disc > 0 ? suggested.Disc : current.Disc,
                        DiscCount = suggested.DiscCount > 0 ? suggested.DiscCount : current.DiscCount,
                        Date = suggested.Date,
                        Year = suggested.Year,
                        OriginalReleaseDate = suggested.OriginalReleaseDate,
                        OriginalYear = suggested.OriginalYear,
                        Publisher = suggested.Publisher,
                        Genres = current.Genres != null && current.Genres.Any() ? current.Genres : new[] { "Audiobook" },
                        Comment = current.Comment,
                        Media = current.Media,
                        Duration = current.Duration,
                        ImageSize = current.ImageSize,
                        Quality = current.Quality,
                        MediaInfo = current.MediaInfo
                    };

                case "readarr":
                default:
                    return suggested;
            }
        }

        private string NormalizeTemplate(string template)
        {
            if (template.IsNullOrWhiteSpace())
            {
                return "readarr";
            }

            if (GetTemplates().Any(x => x.Name == template))
            {
                return template;
            }

            throw new BadRequestException($"Unknown audio tag template '{template}'");
        }

        private string GetTrustedNarratorEvidence(BookFile bookFile, out string warning)
        {
            warning = null;
            var evidence = _contributorEvidenceRepository.GetByBookFileIds(new[] { bookFile.Id })
                .Concat(_contributorEvidenceRepository.GetByEditionIds(new[] { bookFile.EditionId }))
                .Where(x => x.Role == "narrator" && x.DisplayName.IsNotNullOrWhiteSpace())
                .Where(x => IsManualEvidence(x) || IsProviderEvidence(x) || IsHighConfidenceReviewEvidence(x))
                .ToList();

            var selected = evidence
                .OrderByDescending(x => IsManualEvidence(x))
                .ThenByDescending(x => IsProviderEvidence(x))
                .ThenByDescending(x => x.Confidence ?? 0)
                .ThenByDescending(x => x.Updated)
                .FirstOrDefault();

            warning = GetNarratorEvidenceConflictWarning(selected, evidence);

            return selected?.DisplayName;
        }

        private static bool IsManualEvidence(ContributorEvidence evidence)
        {
            return evidence.Source == "manual";
        }

        private static bool IsProviderEvidence(ContributorEvidence evidence)
        {
            return evidence.Source == "providerMetadata";
        }

        private static bool IsHighConfidenceReviewEvidence(ContributorEvidence evidence)
        {
            return (evidence.Source == "aiReview" || evidence.Source == "sttTranscript") && (evidence.Confidence ?? 0) >= 80;
        }

        private static string GetNarratorEvidenceConflictWarning(ContributorEvidence selected, List<ContributorEvidence> evidence)
        {
            if (selected == null || evidence == null)
            {
                return null;
            }

            var providerNames = evidence
                .Where(IsProviderEvidence)
                .Select(x => x.DisplayName)
                .Where(x => x.IsNotNullOrWhiteSpace())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!providerNames.Any())
            {
                return null;
            }

            var nonProviderNames = evidence
                .Where(x => !IsProviderEvidence(x))
                .Select(x => x.DisplayName)
                .Where(x => x.IsNotNullOrWhiteSpace())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!nonProviderNames.Any())
            {
                return null;
            }

            var providerNormalized = providerNames.Select(ContributorEvidence.NormalizeName).ToHashSet();
            var conflictingNonProviderNames = nonProviderNames
                .Where(x => !providerNormalized.Contains(ContributorEvidence.NormalizeName(x)))
                .ToList();

            if (!conflictingNonProviderNames.Any())
            {
                return null;
            }

            return $"Provider narrator metadata ({string.Join(", ", providerNames)}) conflicts with other narrator evidence ({string.Join(", ", conflictingNonProviderNames)}). Selected performer is {selected.DisplayName} from {selected.Source}. Review before writing performer tags.";
        }

        private AudioTagEditPreview BuildPreview(BookFile bookFile, AudioTag current, AudioTag suggested, AudioTag proposed, string warning, List<string> writeWarnings = null)
        {
            return new AudioTagEditPreview
            {
                BookFileId = bookFile.Id,
                Path = bookFile.Path,
                IsAudioFile = true,
                CanWrite = current.IsValid && proposed.IsValid,
                Warning = warning,
                EvidenceWarnings = warning.IsNotNullOrWhiteSpace() && warning.Contains("Provider narrator metadata") ? new List<string> { warning } : new List<string>(),
                WriteWarnings = writeWarnings ?? new List<string>(),
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

        private static List<string> GetWriteWarnings(AudioTag updatedCurrent, AudioTag proposed)
        {
            return updatedCurrent.Diff(proposed)
                .Select(x => $"{x.Key} did not persist. Expected '{x.Value.Item2}', read back '{x.Value.Item1}'.")
                .ToList();
        }

        private static string GetBulkWarning(List<AudioTagEditPreview> files)
        {
            if (files.Any(x => x.Warning.IsNotNullOrWhiteSpace() && x.Warning.Contains("incomplete audiobook part set")))
            {
                return "One or more files belong to an incomplete audiobook part set. Track counts are preserved for those files.";
            }

            if (files.Any(x => x.EvidenceWarnings != null && x.EvidenceWarnings.Any()))
            {
                return "One or more files have provider narrator metadata that conflicts with other narrator evidence. Review the per-file warnings before writing performer tags.";
            }

            if (files.Any(x => x.Warning.IsNotNullOrWhiteSpace()))
            {
                return "One or more files have warnings. Review the per-file details before writing tags.";
            }

            if (files.Any(x => x.WriteWarnings != null && x.WriteWarnings.Any()))
            {
                return "One or more tag writes did not fully persist. Review the per-file warnings before retrying.";
            }

            return null;
        }

        private void LogWriteWarnings(BookFile bookFile, List<string> writeWarnings)
        {
            if (writeWarnings == null || !writeWarnings.Any())
            {
                return;
            }

            _logger.Warn("Audio tag write for book file {0} did not persist {1} field(s): {2}", bookFile.Id, writeWarnings.Count, string.Join("; ", writeWarnings));
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
