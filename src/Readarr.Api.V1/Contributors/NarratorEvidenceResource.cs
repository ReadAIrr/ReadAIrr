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
        public int WorkCount { get; set; }
        public int MatchedBookCount { get; set; }
        public int UnmappedFileCount { get; set; }
        public int ManualEvidenceCount { get; set; }
        public int ReviewEvidenceCount { get; set; }
        public int ProviderEvidenceCount { get; set; }
        public bool HasProviderConfirmedEvidence { get; set; }
        public int? HighestConfidence { get; set; }
        public string ConfidenceLabel { get; set; }
        public string IdentityStatus { get; set; }
        public string IdentityStatusReason { get; set; }
        public bool IsCanonicalIdentity { get; set; }
        public string ReviewOnlyReason { get; set; }
        public string CanonicalDisplayName { get; set; }
        public string CanonicalNormalizedName { get; set; }
        public int AliasCount { get; set; }
        public List<NarratorIdentityLinkResource> Aliases { get; set; }
        public List<NarratorEvidenceSourceCountResource> SourceCounts { get; set; }
        public List<NarratorEvidenceBucketCountResource> BucketCounts { get; set; }
        public DateTime LatestUpdated { get; set; }
        public List<NarratorEvidenceExampleResource> Examples { get; set; }
    }

    public class NarratorEvidenceSourceCountResource
    {
        public string Source { get; set; }
        public int Count { get; set; }
    }

    public class NarratorEvidenceBucketCountResource
    {
        public string Bucket { get; set; }
        public string Label { get; set; }
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

    public class NarratorIdentityLinkResource
    {
        public int Id { get; set; }
        public string CanonicalName { get; set; }
        public string CanonicalNormalizedName { get; set; }
        public string AliasName { get; set; }
        public string AliasNormalizedName { get; set; }
        public string RelationshipType { get; set; }
        public string DisplayPreference { get; set; }
    }

    public class NarratorIdentityLinkUpdateResource
    {
        public string CanonicalName { get; set; }
        public string AliasName { get; set; }
        public string RelationshipType { get; set; }
        public string DisplayPreference { get; set; }
    }

    public static class NarratorEvidenceResourceMapper
    {
        public static List<NarratorEvidenceResource> ToResource(List<ContributorEvidence> evidence, List<BookFile> bookFiles, string term = null, string source = null)
        {
            return ToResource(evidence, bookFiles, new List<NarratorIdentityLink>(), term, source);
        }

        public static List<NarratorEvidenceResource> ToResource(List<ContributorEvidence> evidence, List<BookFile> bookFiles, List<NarratorIdentityLink> aliases, string term = null, string source = null)
        {
            var fileById = (bookFiles ?? new List<BookFile>()).ToDictionary(x => x.Id);
            var aliasMap = new NarratorAliasMap(aliases);

            var narratorEvidence = (evidence ?? new List<ContributorEvidence>())
                .Where(x => x.Role == "narrator" && x.DisplayName.IsNotNullOrWhiteSpace())
                .Where(x => source.IsNullOrWhiteSpace() || x.Source == source)
                .Where(x => term.IsNullOrWhiteSpace() ||
                            x.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            (x.NormalizedName ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            (aliasMap.GetCanonicalName(x) ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            (x.Source ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return narratorEvidence
                .GroupBy(aliasMap.GetCanonicalNormalizedName)
                .Where(x => x.Key.IsNotNullOrWhiteSpace())
                .Select(group =>
                {
                    var ordered = group.OrderByDescending(x => x.Updated).ToList();
                    var groupAliases = aliasMap.GetAliases(group.Key);
                    var canonicalName = aliasMap.GetCanonicalName(group.Key) ??
                                        ordered.GroupBy(x => x.DisplayName)
                                               .OrderByDescending(x => x.Count())
                                               .ThenBy(x => x.Key)
                                               .First()
                                               .Key;

                    return new NarratorEvidenceResource
                    {
                        DisplayName = canonicalName,
                        NormalizedName = group.Key,
                        EvidenceCount = ordered.Count,
                        WorkCount = ordered.Count(x => x.BookFileId.HasValue),
                        MatchedBookCount = ordered.Select(x => ToExampleResource(x, fileById).BookId).Where(x => x.HasValue).Distinct().Count(),
                        UnmappedFileCount = ordered.Count(x =>
                        {
                            fileById.TryGetValue(x.BookFileId ?? 0, out var file);
                            return x.BookFileId.HasValue && (file == null || file.EditionId <= 0);
                        }),
                        ManualEvidenceCount = ordered.Count(x => x.Source == "manual"),
                        ReviewEvidenceCount = ordered.Count(x => x.Source != "manual"),
                        ProviderEvidenceCount = ordered.Count(IsProviderEvidence),
                        HasProviderConfirmedEvidence = ordered.Any(IsProviderEvidence),
                        HighestConfidence = GetHighestConfidence(ordered),
                        ConfidenceLabel = GetConfidenceLabel(ordered),
                        IdentityStatus = GetIdentityStatus(ordered, groupAliases),
                        IdentityStatusReason = GetIdentityStatusReason(ordered, groupAliases),
                        IsCanonicalIdentity = groupAliases.Any(),
                        ReviewOnlyReason = groupAliases.Any() ?
                            "Narrator identity is user-standardized from linked evidence. Source evidence is preserved and no metadata or tags have been written automatically." :
                            "Narrator identity is assembled from available evidence. Provider metadata only counts when the source supplies narrator-like role/name data.",
                        CanonicalDisplayName = canonicalName,
                        CanonicalNormalizedName = group.Key,
                        AliasCount = groupAliases.Count,
                        Aliases = groupAliases.ToResource(),
                        LatestUpdated = ordered.Max(x => x.Updated),
                        SourceCounts = ordered.GroupBy(x => x.Source)
                                              .OrderBy(x => x.Key)
                                              .Select(x => new NarratorEvidenceSourceCountResource
                                              {
                                                  Source = x.Key,
                                                  Count = x.Count()
                                              })
                                              .ToList(),
                        BucketCounts = GetBucketCounts(ordered, fileById, groupAliases),
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
            return ToDetailResource(evidence, bookFiles, new List<NarratorIdentityLink>(), normalizedName, source);
        }

        public static NarratorEvidenceDetailResource ToDetailResource(List<ContributorEvidence> evidence, List<BookFile> bookFiles, List<NarratorIdentityLink> aliases, string normalizedName, string source = null)
        {
            var fileById = (bookFiles ?? new List<BookFile>()).ToDictionary(x => x.Id);
            var aliasMap = new NarratorAliasMap(aliases);
            var canonicalNormalizedName = aliasMap.GetCanonicalNormalizedName(normalizedName);

            var narratorEvidence = (evidence ?? new List<ContributorEvidence>())
                .Where(x => x.Role == "narrator" && x.DisplayName.IsNotNullOrWhiteSpace())
                .Where(x => source.IsNullOrWhiteSpace() || x.Source == source)
                .Where(x =>
                {
                    return aliasMap.GetCanonicalNormalizedName(x) == canonicalNormalizedName;
                })
                .OrderByDescending(x => x.Updated)
                .ToList();

            if (!narratorEvidence.Any())
            {
                return null;
            }

            var summary = ToResource(narratorEvidence, bookFiles, aliases).First();

            return new NarratorEvidenceDetailResource
            {
                DisplayName = summary.DisplayName,
                NormalizedName = summary.NormalizedName,
                EvidenceCount = summary.EvidenceCount,
                WorkCount = summary.WorkCount,
                MatchedBookCount = summary.MatchedBookCount,
                UnmappedFileCount = summary.UnmappedFileCount,
                ManualEvidenceCount = summary.ManualEvidenceCount,
                ReviewEvidenceCount = summary.ReviewEvidenceCount,
                ProviderEvidenceCount = summary.ProviderEvidenceCount,
                HasProviderConfirmedEvidence = summary.HasProviderConfirmedEvidence,
                HighestConfidence = summary.HighestConfidence,
                ConfidenceLabel = summary.ConfidenceLabel,
                IdentityStatus = summary.IdentityStatus,
                IdentityStatusReason = summary.IdentityStatusReason,
                IsCanonicalIdentity = summary.IsCanonicalIdentity,
                ReviewOnlyReason = summary.ReviewOnlyReason,
                CanonicalDisplayName = summary.CanonicalDisplayName,
                CanonicalNormalizedName = summary.CanonicalNormalizedName,
                AliasCount = summary.AliasCount,
                Aliases = summary.Aliases,
                LatestUpdated = summary.LatestUpdated,
                SourceCounts = summary.SourceCounts,
                BucketCounts = summary.BucketCounts,
                Examples = summary.Examples,
                Works = narratorEvidence.Select(x => ToExampleResource(x, fileById)).ToList()
            };
        }

        public static NarratorIdentityLinkResource ToResource(this NarratorIdentityLink model)
        {
            return new NarratorIdentityLinkResource
            {
                Id = model.Id,
                CanonicalName = model.CanonicalName,
                CanonicalNormalizedName = model.CanonicalNormalizedName,
                AliasName = model.AliasName,
                AliasNormalizedName = model.AliasNormalizedName,
                RelationshipType = model.RelationshipType,
                DisplayPreference = model.DisplayPreference
            };
        }

        public static List<NarratorIdentityLinkResource> ToResource(this IEnumerable<NarratorIdentityLink> models)
        {
            return models.Select(ToResource).ToList();
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

        private static List<NarratorEvidenceBucketCountResource> GetBucketCounts(List<ContributorEvidence> evidence, Dictionary<int, BookFile> fileById, List<NarratorIdentityLink> aliases)
        {
            var matchedBookIds = evidence.Select(x => ToExampleResource(x, fileById).BookId).Where(x => x.HasValue).Distinct().Count();
            var importedFileIds = evidence.Where(x =>
            {
                fileById.TryGetValue(x.BookFileId ?? 0, out var file);
                return file != null && file.EditionId > 0;
            }).Select(x => x.BookFileId.Value).Distinct().Count();
            var unmappedFileIds = evidence.Where(x =>
            {
                fileById.TryGetValue(x.BookFileId ?? 0, out var file);
                return x.BookFileId.HasValue && (file == null || file.EditionId <= 0);
            }).Select(x => x.BookFileId.Value).Distinct().Count();

            return new List<NarratorEvidenceBucketCountResource>
            {
                new NarratorEvidenceBucketCountResource { Bucket = "matchedLibraryBooks", Label = "Matched library books", Count = matchedBookIds },
                new NarratorEvidenceBucketCountResource { Bucket = "importedFiles", Label = "Imported files", Count = importedFileIds },
                new NarratorEvidenceBucketCountResource { Bucket = "unmappedFiles", Label = "Unmapped files", Count = unmappedFileIds },
                new NarratorEvidenceBucketCountResource { Bucket = "providerEvidence", Label = "Provider evidence", Count = evidence.Count(x => x.Source == "providerMetadata") },
                new NarratorEvidenceBucketCountResource { Bucket = "manualEvidence", Label = "Manual evidence", Count = evidence.Count(x => x.Source == "manual") },
                new NarratorEvidenceBucketCountResource { Bucket = "reviewEvidence", Label = "AI/STT review evidence", Count = evidence.Count(x => x.Source == "aiReview" || x.Source == "sttTranscript") },
                new NarratorEvidenceBucketCountResource { Bucket = "aliases", Label = "Linked aliases", Count = aliases.Count }
            };
        }

        private static bool IsProviderEvidence(ContributorEvidence evidence)
        {
            return evidence.Source == "providerMetadata";
        }

        private static string GetConfidenceLabel(List<ContributorEvidence> evidence)
        {
            if (evidence.Any(IsProviderEvidence))
            {
                return "Provider-confirmed";
            }

            if (evidence.Any(x => x.Source == "manual"))
            {
                return "Manual evidence";
            }

            var highest = evidence.Where(x => x.Confidence.HasValue).Select(x => x.Confidence.Value).DefaultIfEmpty(0).Max();

            if (highest >= 85)
            {
                return "High-confidence review";
            }

            if (highest >= 60)
            {
                return "Medium-confidence review";
            }

            return "Needs review";
        }

        private static int? GetHighestConfidence(List<ContributorEvidence> evidence)
        {
            var confidenceValues = evidence.Where(x => x.Confidence.HasValue).Select(x => x.Confidence.Value).ToList();

            return confidenceValues.Any() ? confidenceValues.Max() : null;
        }

        private static string GetIdentityStatus(List<ContributorEvidence> evidence, List<NarratorIdentityLink> aliases)
        {
            if (evidence.Any(IsProviderEvidence))
            {
                return "Provider confirmed";
            }

            if (aliases.Any())
            {
                return "Linked identity";
            }

            if (evidence.Any(x => x.Source == "manual"))
            {
                return "Manual evidence";
            }

            return "Review evidence";
        }

        private static string GetIdentityStatusReason(List<ContributorEvidence> evidence, List<NarratorIdentityLink> aliases)
        {
            if (evidence.Any(IsProviderEvidence))
            {
                return "At least one metadata source supplied narrator-like role/name data for this identity.";
            }

            if (aliases.Any())
            {
                return "This identity is grouped by user-created narrator alias links.";
            }

            if (evidence.Any(x => x.Source == "manual"))
            {
                return "This identity includes user-entered narrator evidence.";
            }

            return "This identity is assembled from AI/STT review evidence and still needs human confirmation.";
        }

        private class NarratorAliasMap
        {
            private readonly Dictionary<string, NarratorIdentityLink> _aliasByNormalizedName;
            private readonly Dictionary<string, List<NarratorIdentityLink>> _aliasesByCanonicalName;

            public NarratorAliasMap(List<NarratorIdentityLink> aliases)
            {
                var validAliases = (aliases ?? new List<NarratorIdentityLink>())
                    .Where(x => x.CanonicalNormalizedName.IsNotNullOrWhiteSpace() && x.AliasNormalizedName.IsNotNullOrWhiteSpace())
                    .ToList();

                _aliasByNormalizedName = validAliases
                    .GroupBy(x => x.AliasNormalizedName)
                    .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.Id).First());

                _aliasesByCanonicalName = validAliases
                    .GroupBy(x => x.CanonicalNormalizedName)
                    .ToDictionary(x => x.Key, x => x.OrderBy(y => y.AliasName).ToList());
            }

            public string GetCanonicalNormalizedName(ContributorEvidence evidence)
            {
                var normalized = evidence.NormalizedName.IsNotNullOrWhiteSpace() ? evidence.NormalizedName : ContributorEvidence.NormalizeName(evidence.DisplayName);
                return GetCanonicalNormalizedName(normalized);
            }

            public string GetCanonicalNormalizedName(string normalizedName)
            {
                var normalized = ContributorEvidence.NormalizeName(normalizedName);

                return _aliasByNormalizedName.TryGetValue(normalized, out var link) ?
                    link.CanonicalNormalizedName :
                    normalized;
            }

            public string GetCanonicalName(ContributorEvidence evidence)
            {
                return GetCanonicalName(GetCanonicalNormalizedName(evidence));
            }

            public string GetCanonicalName(string canonicalNormalizedName)
            {
                if (_aliasesByCanonicalName.TryGetValue(canonicalNormalizedName, out var links))
                {
                    return links.First().CanonicalName;
                }

                return null;
            }

            public List<NarratorIdentityLink> GetAliases(string canonicalNormalizedName)
            {
                return _aliasesByCanonicalName.TryGetValue(canonicalNormalizedName, out var links) ?
                    links :
                    new List<NarratorIdentityLink>();
            }
        }
    }
}
