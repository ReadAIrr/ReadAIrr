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
        public List<MetadataComparisonFieldResource> Fields { get; set; }
    }

    public class MetadataComparisonFieldResource
    {
        public string Field { get; set; }
        public string Label { get; set; }
        public string Status { get; set; }
        public string LocalValue { get; set; }
        public string ProviderValue { get; set; }
        public string EvidenceValue { get; set; }
        public string Source { get; set; }
        public int Confidence { get; set; }
        public string Explanation { get; set; }
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
                Compare("title", "Book title", localBook?.Title, providerBook?.Title, "book metadata"),
                Compare("author", "Author", localBook?.AuthorMetadata?.Value?.Name, providerBook?.AuthorMetadata?.Value?.Name, "author metadata"),
                Compare("editionTitle", "Edition title", localEdition?.Title, providerEdition?.Title, "edition metadata"),
                Compare("language", "Language", localEdition?.Language, providerEdition?.Language, "edition metadata"),
                Compare("isbn13", "ISBN-13", localEdition?.Isbn13, providerEdition?.Isbn13, "edition identifiers"),
                Compare("asin", "ASIN", localEdition?.Asin, providerEdition?.Asin, "edition identifiers"),
                Compare("publisher", "Publisher", localEdition?.Publisher, providerEdition?.Publisher, "edition metadata"),
                Compare("pageCount", "Page count", FormatNumber(localEdition?.PageCount), FormatNumber(providerEdition?.PageCount), "edition metadata"),
                Compare("publicationDate", "Publication date", FormatDate(localEdition?.ReleaseDate ?? localBook?.ReleaseDate), FormatDate(providerEdition?.ReleaseDate ?? providerBook?.ReleaseDate), "release metadata"),
                Compare("series", "Series", FormatSeries(localBook), FormatSeries(providerBook), "series metadata"),
                Compare("overview", "Overview", localEdition?.Overview, providerEdition?.Overview, "description metadata", true),
                Compare("cover", "Cover artwork", HasImages(localEdition) ? "present" : null, HasImages(providerEdition) ? "present" : null, "cover metadata")
            };

            fields.Add(CompareNarrators(evidence));

            var reviewFields = fields.Count(x => x.Status == "conflicting" || x.Status == "provider-only" || x.Status == "missing" || x.Status == "needs-review" || x.Status == "low-confidence");
            var confirmedFields = fields.Count(x => x.Status == "confirmed");
            var summaryStatus = reviewFields > 0 ? "needs-review" : "confirmed";

            if (providerBook == null && providerError.IsNotNullOrWhiteSpace())
            {
                summaryStatus = "needs-review";
                fields.Insert(0, new MetadataComparisonFieldResource
                {
                    Field = "providerAvailability",
                    Label = "Provider metadata",
                    Status = "needs-review",
                    Source = metadataSource,
                    Confidence = 0,
                    Explanation = providerError
                });
            }

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
                Fields = fields
            };
        }

        private static MetadataComparisonFieldResource Compare(string field, string label, string localValue, string providerValue, string source, bool longValue = false)
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
                Status = status,
                LocalValue = localValue,
                ProviderValue = providerValue,
                Source = source,
                Confidence = confidence,
                Explanation = Explain(label, status)
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
                Status = status,
                LocalValue = local,
                ProviderValue = provider,
                EvidenceValue = FormatEvidenceSources(narratorEvidence),
                Source = "contributor evidence",
                Confidence = status == "confirmed" ? 90 : status == "missing" ? 0 : 50,
                Explanation = status == "confirmed" ?
                    "Narrator evidence agrees across available sources." :
                    "Narrator evidence is review-only and should be confirmed before using it for library decisions."
            };
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
