using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;

namespace Readarr.Api.V1.Metadata
{
    public class MetadataComparisonResource
    {
        public int BookId { get; set; }
        public string Title { get; set; }
        public string MetadataSource { get; set; }
        public bool ProviderAvailable { get; set; }
        public string ProviderStatus { get; set; }
        public string SummaryStatus { get; set; }
        public string Summary { get; set; }
        public MetadataConfidenceScoringResource ConfidenceScoring { get; set; }
        public List<MetadataComparisonStatusCountResource> StatusCounts { get; set; }
        public List<MetadataComparisonFieldResource> Fields { get; set; }
    }

    public class MetadataConfidenceScoringResource
    {
        public string Mode { get; set; }
        public string Summary { get; set; }
        public bool AutomaticMetadataDecisioningEnabled { get; set; }
        public bool AiDecisioningEnabled { get; set; }
        public int FieldConfidenceScore { get; set; }
        public int ConfirmedFields { get; set; }
        public int ReviewFields { get; set; }
        public int LocalOnlyFields { get; set; }
        public int ProviderOnlyFields { get; set; }
        public int EvidenceFields { get; set; }
        public List<MetadataConfidenceSourceResource> Sources { get; set; }
    }

    public class MetadataConfidenceSourceResource
    {
        public string SourceType { get; set; }
        public string SourceLabel { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public bool IsAvailable { get; set; }
        public int Weight { get; set; }
        public string Status { get; set; }
        public string Explanation { get; set; }
    }

    public class MetadataComparisonStatusCountResource
    {
        public string Status { get; set; }
        public string Label { get; set; }
        public int Count { get; set; }
    }

    public class MetadataComparisonFieldResource
    {
        public string Field { get; set; }
        public string Label { get; set; }
        public string Section { get; set; }
        public string SectionLabel { get; set; }
        public string Status { get; set; }
        public string LocalValue { get; set; }
        public string ProviderValue { get; set; }
        public string EvidenceValue { get; set; }
        public string Source { get; set; }
        public int Confidence { get; set; }
        public string Explanation { get; set; }
        public string ActionHint { get; set; }
    }

    public static class MetadataComparisonResourceMapper
    {
        public static MetadataComparisonResource ToResource(Book localBook,
                                                            Edition localEdition,
                                                            Book providerBook,
                                                            Edition providerEdition,
                                                            List<ContributorEvidence> evidence,
                                                            string metadataSource,
                                                            string providerError = null)
        {
            var fields = new List<MetadataComparisonFieldResource>
            {
                Compare("title", "Book title", "coreIdentity", "Core book identity", localBook?.Title, providerBook?.Title, "book metadata"),
                Compare("author", "Author", "coreIdentity", "Core book identity", localBook?.AuthorMetadata?.Value?.Name, providerBook?.AuthorMetadata?.Value?.Name, "author metadata"),
                Compare("editionTitle", "Edition title", "editionIdentifiers", "Edition identifiers", localEdition?.Title, providerEdition?.Title, "edition metadata"),
                Compare("language", "Language", "editionIdentifiers", "Edition identifiers", localEdition?.Language, providerEdition?.Language, "edition metadata"),
                Compare("isbn13", "ISBN-13", "editionIdentifiers", "Edition identifiers", localEdition?.Isbn13, providerEdition?.Isbn13, "edition identifiers"),
                Compare("asin", "ASIN", "editionIdentifiers", "Edition identifiers", localEdition?.Asin, providerEdition?.Asin, "edition identifiers"),
                Compare("publisher", "Publisher", "publication", "Publication details", localEdition?.Publisher, providerEdition?.Publisher, "edition metadata"),
                Compare("pageCount", "Page count", "publication", "Publication details", FormatNumber(localEdition?.PageCount), FormatNumber(providerEdition?.PageCount), "edition metadata"),
                Compare("publicationDate", "Publication date", "publication", "Publication details", FormatDate(localEdition?.ReleaseDate ?? localBook?.ReleaseDate), FormatDate(providerEdition?.ReleaseDate ?? providerBook?.ReleaseDate), "release metadata"),
                Compare("series", "Series", "series", "Series", FormatSeries(localBook), FormatSeries(providerBook), "series metadata"),
                Compare("overview", "Overview", "coverOverview", "Cover and overview", localEdition?.Overview, providerEdition?.Overview, "description metadata", true),
                Compare("cover", "Cover artwork", "coverOverview", "Cover and overview", HasImages(localEdition) ? "present" : null, HasImages(providerEdition) ? "present" : null, "cover metadata")
            };

            fields.Add(CompareNarrators(evidence));

            var summaryStatus = fields.Any(x => IsReviewStatus(x.Status)) ? "needs-review" : "confirmed";

            if (providerBook == null && providerError.IsNotNullOrWhiteSpace())
            {
                summaryStatus = "needs-review";
                fields.Insert(0, new MetadataComparisonFieldResource
                {
                    Field = "providerAvailability",
                    Label = "Provider metadata",
                    Section = "provider",
                    SectionLabel = "Provider availability",
                    Status = "needs-review",
                    Source = metadataSource,
                    Confidence = 0,
                    Explanation = providerError,
                    ActionHint = "Check provider reachability or review this book using local metadata and stored evidence only."
                });
            }

            var reviewFields = fields.Count(x => IsReviewStatus(x.Status));
            var confirmedFields = fields.Count(x => x.Status == "confirmed");

            return new MetadataComparisonResource
            {
                BookId = localBook.Id,
                Title = localBook.Title,
                MetadataSource = metadataSource,
                ProviderAvailable = providerBook != null,
                ProviderStatus = providerBook != null ? "Provider metadata was available for comparison." : providerError ?? "Provider metadata was not available for comparison.",
                SummaryStatus = summaryStatus,
                Summary = reviewFields > 0 ?
                    $"{reviewFields} field{(reviewFields == 1 ? string.Empty : "s")} need review; {confirmedFields} confirmed." :
                    $"{confirmedFields} field{(confirmedFields == 1 ? string.Empty : "s")} confirmed; no disagreements found.",
                ConfidenceScoring = BuildConfidenceScoring(fields, metadataSource, providerBook != null),
                StatusCounts = BuildStatusCounts(fields),
                Fields = fields
            };
        }

        private static MetadataConfidenceScoringResource BuildConfidenceScoring(List<MetadataComparisonFieldResource> fields, string metadataSource, bool providerAvailable)
        {
            var confirmedFields = fields.Count(x => x.Status == "confirmed");
            var reviewFields = fields.Count(x => IsReviewStatus(x.Status));
            var localOnlyFields = fields.Count(x => x.Status == "local-only");
            var providerOnlyFields = fields.Count(x => x.Status == "provider-only");
            var evidenceFields = fields.Count(x => x.EvidenceValue.IsNotNullOrWhiteSpace());
            var comparableFields = fields.Count(x => x.Field != "providerAvailability");
            var fieldConfidenceScore = comparableFields > 0 ? (int)Math.Round(confirmedFields * 100.0 / comparableFields) : 0;

            return new MetadataConfidenceScoringResource
            {
                Mode = "passiveSingleProvider",
                Summary = providerAvailable ?
                    $"Passive confidence score is {fieldConfidenceScore}% based on {confirmedFields} confirmed field{(confirmedFields == 1 ? string.Empty : "s")} out of {comparableFields} comparable field{(comparableFields == 1 ? string.Empty : "s")}. Automatic metadata and AI decisioning are disabled." :
                    "Passive confidence score is limited because provider metadata is unavailable. Automatic metadata and AI decisioning are disabled.",
                AutomaticMetadataDecisioningEnabled = false,
                AiDecisioningEnabled = false,
                FieldConfidenceScore = fieldConfidenceScore,
                ConfirmedFields = confirmedFields,
                ReviewFields = reviewFields,
                LocalOnlyFields = localOnlyFields,
                ProviderOnlyFields = providerOnlyFields,
                EvidenceFields = evidenceFields,
                Sources = BuildConfidenceSources(metadataSource, providerAvailable, evidenceFields)
            };
        }

        private static List<MetadataConfidenceSourceResource> BuildConfidenceSources(string metadataSource, bool providerAvailable, int evidenceFields)
        {
            return new List<MetadataConfidenceSourceResource>
            {
                new MetadataConfidenceSourceResource
                {
                    SourceType = "localLibrary",
                    SourceLabel = "Local library",
                    Role = "baseline",
                    IsActive = true,
                    IsAvailable = true,
                    Weight = 50,
                    Status = "available",
                    Explanation = "Current ReadAIrr book and edition metadata. This is compared passively and is never overwritten by this review."
                },
                new MetadataConfidenceSourceResource
                {
                    SourceType = "activeProvider",
                    SourceLabel = metadataSource.IsNotNullOrWhiteSpace() ? metadataSource : "Configured metadata provider",
                    Role = "activeProvider",
                    IsActive = true,
                    IsAvailable = providerAvailable,
                    Weight = providerAvailable ? 40 : 0,
                    Status = providerAvailable ? "available" : "unavailable",
                    Explanation = providerAvailable ?
                        "Current configured provider metadata used as read-only comparison evidence." :
                        "Provider metadata was not available, so this comparison relies on local metadata and stored evidence."
                },
                new MetadataConfidenceSourceResource
                {
                    SourceType = "contributorEvidence",
                    SourceLabel = "Contributor evidence",
                    Role = "supportingEvidence",
                    IsActive = evidenceFields > 0,
                    IsAvailable = evidenceFields > 0,
                    Weight = evidenceFields > 0 ? 10 : 0,
                    Status = evidenceFields > 0 ? "available" : "notAvailable",
                    Explanation = evidenceFields > 0 ?
                        "Stored narrator/contributor evidence is included as review-only supporting context." :
                        "No stored contributor evidence was available for this book's editions."
                },
                new MetadataConfidenceSourceResource
                {
                    SourceType = "futureReferenceProvider",
                    SourceLabel = "Future reference provider",
                    Role = "futureReference",
                    IsActive = false,
                    IsAvailable = false,
                    Weight = 0,
                    Status = "notEvaluated",
                    Explanation = "Reserved for future cross-provider scoring. This stub does not query an additional provider."
                },
                new MetadataConfidenceSourceResource
                {
                    SourceType = "aiReview",
                    SourceLabel = "Optional AI review",
                    Role = "futureManualReview",
                    IsActive = false,
                    IsAvailable = false,
                    Weight = 0,
                    Status = "disabled",
                    Explanation = "Reserved for future opt-in review suggestions. AI decisioning is disabled and cannot auto-apply metadata."
                }
            };
        }

        private static MetadataComparisonFieldResource Compare(string field, string label, string section, string sectionLabel, string localValue, string providerValue, string source, bool longValue = false)
        {
            localValue = NormalizeDisplay(localValue, longValue);
            providerValue = NormalizeDisplay(providerValue, longValue);

            var status = GetStatus(localValue, providerValue);
            var confidence = status == "confirmed" ? 100 :
                status == "conflicting" ? 40 :
                status == "provider-only" ? 60 :
                status == "local-only" ? 70 :
                0;

            return new MetadataComparisonFieldResource
            {
                Field = field,
                Label = label,
                Section = section,
                SectionLabel = sectionLabel,
                Status = status,
                LocalValue = localValue,
                ProviderValue = providerValue,
                Source = source,
                Confidence = confidence,
                Explanation = Explain(label, status),
                ActionHint = ActionHint(label, status)
            };
        }

        private static MetadataComparisonFieldResource CompareNarrators(List<ContributorEvidence> evidence)
        {
            var narratorEvidence = evidence
                .Where(x => x.Role == "narrator" && x.DisplayName.IsNotNullOrWhiteSpace())
                .ToList();

            var provider = FormatNames(narratorEvidence.Where(x => x.Source == "providerMetadata"));
            var local = FormatNames(narratorEvidence.Where(x => x.Source != "providerMetadata"));
            var status = GetStatus(local, provider);

            if (status == "local-only" || status == "provider-only")
            {
                status = "needs-review";
            }

            return new MetadataComparisonFieldResource
            {
                Field = "narrators",
                Label = "Narrator evidence",
                Section = "contributors",
                SectionLabel = "Narrator and contributor evidence",
                Status = status,
                LocalValue = local,
                ProviderValue = provider,
                EvidenceValue = FormatEvidenceSources(narratorEvidence),
                Source = "contributor evidence",
                Confidence = status == "confirmed" ? 90 : status == "missing" ? 0 : 50,
                Explanation = status == "confirmed" ?
                    "Narrator evidence agrees across available sources." :
                    "Narrator evidence is review-only and should be confirmed before using it for library decisions.",
                ActionHint = status == "confirmed" ?
                    "No review needed unless the narrator identity looks wrong." :
                    "Review narrator evidence before relying on it for matching, tagging, or completeness decisions."
            };
        }

        private static List<MetadataComparisonStatusCountResource> BuildStatusCounts(List<MetadataComparisonFieldResource> fields)
        {
            return fields.GroupBy(x => x.Status)
                .Select(x => new MetadataComparisonStatusCountResource
                {
                    Status = x.Key,
                    Label = StatusLabel(x.Key),
                    Count = x.Count()
                })
                .OrderBy(x => StatusSortOrder(x.Status))
                .ThenBy(x => x.Label)
                .ToList();
        }

        private static bool IsReviewStatus(string status)
        {
            return status == "conflicting" ||
                   status == "provider-only" ||
                   status == "missing" ||
                   status == "needs-review" ||
                   status == "low-confidence";
        }

        private static string GetStatus(string localValue, string providerValue)
        {
            var hasLocal = localValue.IsNotNullOrWhiteSpace();
            var hasProvider = providerValue.IsNotNullOrWhiteSpace();

            if (!hasLocal && !hasProvider)
            {
                return "missing";
            }

            if (!hasLocal)
            {
                return "provider-only";
            }

            if (!hasProvider)
            {
                return "local-only";
            }

            return string.Equals(localValue, providerValue, StringComparison.InvariantCultureIgnoreCase) ? "confirmed" : "conflicting";
        }

        private static string Explain(string label, string status)
        {
            switch (status)
            {
                case "confirmed":
                    return $"{label} matches the available provider metadata.";
                case "conflicting":
                    return $"{label} differs between the local library and provider metadata.";
                case "provider-only":
                    return $"{label} exists in provider metadata but is missing locally.";
                case "local-only":
                    return $"{label} exists locally but was not present in the provider metadata.";
                default:
                    return $"{label} is missing from both local and provider metadata.";
            }
        }

        private static string ActionHint(string label, string status)
        {
            switch (status)
            {
                case "confirmed":
                    return "No action needed unless the matched metadata source is wrong.";
                case "conflicting":
                    return $"Review {label.ToLowerInvariant()} before accepting provider metadata or editing local metadata.";
                case "provider-only":
                    return $"Provider has {label.ToLowerInvariant()}; review before filling the local value.";
                case "local-only":
                    return $"Keep the local {label.ToLowerInvariant()} if it is intentional, or verify the provider record.";
                default:
                    return $"Add or verify {label.ToLowerInvariant()} if it matters for matching, completeness, or display.";
            }
        }

        private static string StatusLabel(string status)
        {
            switch (status)
            {
                case "confirmed":
                    return "Confirmed";
                case "conflicting":
                    return "Conflicting";
                case "provider-only":
                    return "Provider only";
                case "local-only":
                    return "Local only";
                case "needs-review":
                    return "Needs review";
                case "low-confidence":
                    return "Low confidence";
                default:
                    return "Missing";
            }
        }

        private static int StatusSortOrder(string status)
        {
            switch (status)
            {
                case "needs-review":
                    return 0;
                case "conflicting":
                    return 1;
                case "provider-only":
                    return 2;
                case "missing":
                    return 3;
                case "local-only":
                    return 4;
                case "low-confidence":
                    return 5;
                case "confirmed":
                    return 6;
                default:
                    return 7;
            }
        }

        private static string NormalizeDisplay(string value, bool longValue)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return null;
            }

            value = value.Trim();

            if (longValue && value.Length > 320)
            {
                return value.Substring(0, 320) + "...";
            }

            return value;
        }

        private static string FormatNumber(int? value)
        {
            return value.GetValueOrDefault() > 0 ? value.Value.ToString() : null;
        }

        private static string FormatDate(DateTime? value)
        {
            return value?.ToString("yyyy-MM-dd");
        }

        private static string FormatSeries(Book book)
        {
            return book?.SeriesLinks?.Value?
                .Where(x => x.Series?.Value?.Title.IsNotNullOrWhiteSpace() ?? false)
                .OrderBy(x => x.SeriesPosition)
                .Select(x => x.Series.Value.Title + (x.Position.IsNotNullOrWhiteSpace() ? $" #{x.Position}" : string.Empty))
                .ConcatToString("; ");
        }

        private static bool HasImages(Edition edition)
        {
            return edition?.Images?.Any() == true;
        }

        private static string FormatNames(IEnumerable<ContributorEvidence> evidence)
        {
            return evidence.Select(x => x.DisplayName)
                .Where(x => x.IsNotNullOrWhiteSpace())
                .Distinct()
                .OrderBy(x => x)
                .ConcatToString("; ");
        }

        private static string FormatEvidenceSources(IEnumerable<ContributorEvidence> evidence)
        {
            return evidence.GroupBy(x => x.Source.IsNotNullOrWhiteSpace() ? x.Source : "unknown")
                .Select(x => $"{x.Key}: {x.Count()}")
                .OrderBy(x => x)
                .ConcatToString("; ");
        }
    }
}
