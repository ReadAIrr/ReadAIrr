using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles;

namespace Readarr.Api.V1.Contributors
{
    public class NarratorEvidenceResource
    {
        public string DisplayName { get; set; }
        public string NormalizedName { get; set; }
        public int EvidenceCount { get; set; }
        public List<NarratorEvidenceSourceCountResource> SourceCounts { get; set; }
        public DateTime LatestUpdated { get; set; }
        public List<NarratorEvidenceExampleResource> Examples { get; set; }
    }

    public class NarratorEvidenceSourceCountResource
    {
        public string Source { get; set; }
        public int Count { get; set; }
    }

    public class NarratorEvidenceExampleResource
    {
        public int EvidenceId { get; set; }
        public int? BookFileId { get; set; }
        public int? BookId { get; set; }
        public string BookTitle { get; set; }
        public string BookTitleSlug { get; set; }
        public int? AuthorId { get; set; }
        public string AuthorName { get; set; }
        public string AuthorTitleSlug { get; set; }
        public int? EditionId { get; set; }
        public string EditionTitle { get; set; }
        public string Path { get; set; }
        public string Source { get; set; }
        public int? Confidence { get; set; }
        public DateTime Updated { get; set; }
    }

    public class NarratorEvidenceDetailResource : NarratorEvidenceResource
    {
        public List<NarratorEvidenceExampleResource> Works { get; set; }
    }

    public static class NarratorEvidenceResourceMapper
    {
        public static List<NarratorEvidenceResource> ToResource(List<ContributorEvidence> evidence, List<BookFile> bookFiles, string term = null, string source = null)
        {
            var fileById = (bookFiles ?? new List<BookFile>()).ToDictionary(x => x.Id);

            var narratorEvidence = (evidence ?? new List<ContributorEvidence>())
                .Where(x => x.Role == "narrator" && x.DisplayName.IsNotNullOrWhiteSpace())
                .Where(x => source.IsNullOrWhiteSpace() || x.Source == source)
                .Where(x => term.IsNullOrWhiteSpace() ||
                            x.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            (x.NormalizedName ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            (x.Source ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return narratorEvidence
                .GroupBy(x => x.NormalizedName.IsNotNullOrWhiteSpace() ? x.NormalizedName : ContributorEvidence.NormalizeName(x.DisplayName))
                .Where(x => x.Key.IsNotNullOrWhiteSpace())
                .Select(group =>
                {
                    var ordered = group.OrderByDescending(x => x.Updated).ToList();

                    return new NarratorEvidenceResource
                    {
                        DisplayName = ordered.GroupBy(x => x.DisplayName)
                                             .OrderByDescending(x => x.Count())
                                             .ThenBy(x => x.Key)
                                             .First()
                                             .Key,
                        NormalizedName = group.Key,
                        EvidenceCount = ordered.Count,
                        LatestUpdated = ordered.Max(x => x.Updated),
                        SourceCounts = ordered.GroupBy(x => x.Source)
                                              .OrderBy(x => x.Key)
                                              .Select(x => new NarratorEvidenceSourceCountResource
                                              {
                                                  Source = x.Key,
                                                  Count = x.Count()
                                              })
                                              .ToList(),
                        Examples = ordered.Take(5)
                                          .Select(x => ToExampleResource(x, fileById))
                                          .ToList()
                    };
                })
                .OrderBy(x => x.DisplayName)
                .ToList();
        }

        public static NarratorEvidenceDetailResource ToDetailResource(List<ContributorEvidence> evidence, List<BookFile> bookFiles, string normalizedName, string source = null)
        {
            var fileById = (bookFiles ?? new List<BookFile>()).ToDictionary(x => x.Id);

            var narratorEvidence = (evidence ?? new List<ContributorEvidence>())
                .Where(x => x.Role == "narrator" && x.DisplayName.IsNotNullOrWhiteSpace())
                .Where(x => source.IsNullOrWhiteSpace() || x.Source == source)
                .Where(x =>
                {
                    var normalized = x.NormalizedName.IsNotNullOrWhiteSpace() ? x.NormalizedName : ContributorEvidence.NormalizeName(x.DisplayName);
                    return normalized == normalizedName;
                })
                .OrderByDescending(x => x.Updated)
                .ToList();

            if (!narratorEvidence.Any())
            {
                return null;
            }

            var summary = ToResource(narratorEvidence, bookFiles).First();

            return new NarratorEvidenceDetailResource
            {
                DisplayName = summary.DisplayName,
                NormalizedName = summary.NormalizedName,
                EvidenceCount = summary.EvidenceCount,
                LatestUpdated = summary.LatestUpdated,
                SourceCounts = summary.SourceCounts,
                Examples = summary.Examples,
                Works = narratorEvidence.Select(x => ToExampleResource(x, fileById)).ToList()
            };
        }

        private static NarratorEvidenceExampleResource ToExampleResource(ContributorEvidence evidence, Dictionary<int, BookFile> fileById)
        {
            fileById.TryGetValue(evidence.BookFileId ?? 0, out var file);

            var edition = file?.Edition?.Value;
            var book = edition?.Book?.Value;
            var author = file?.Author?.Value ?? book?.Author?.Value;
            var authorMetadata = author?.Metadata?.Value;

            return new NarratorEvidenceExampleResource
            {
                EvidenceId = evidence.Id,
                BookFileId = evidence.BookFileId,
                BookId = book?.Id,
                BookTitle = book?.Title,
                BookTitleSlug = book?.TitleSlug,
                AuthorId = author?.Id,
                AuthorName = author?.Name,
                AuthorTitleSlug = authorMetadata?.TitleSlug,
                EditionId = evidence.EditionId ?? edition?.Id,
                EditionTitle = edition?.Title,
                Path = file?.Path,
                Source = evidence.Source,
                Confidence = evidence.Confidence,
                Updated = evidence.Updated
            };
        }
    }
}
