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
        public int? BookFileId { get; set; }
        public string Path { get; set; }
        public string Source { get; set; }
        public int? Confidence { get; set; }
        public DateTime Updated { get; set; }
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
                                          .Select(x =>
                                          {
                                              fileById.TryGetValue(x.BookFileId ?? 0, out var file);

                                              return new NarratorEvidenceExampleResource
                                              {
                                                  BookFileId = x.BookFileId,
                                                  Path = file?.Path,
                                                  Source = x.Source,
                                                  Confidence = x.Confidence,
                                                  Updated = x.Updated
                                              };
                                          })
                                          .ToList()
                    };
                })
                .OrderBy(x => x.DisplayName)
                .ToList();
        }
    }
}
