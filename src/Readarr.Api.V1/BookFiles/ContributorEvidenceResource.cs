using System;
using NzbDrone.Core.MediaFiles;

namespace Readarr.Api.V1.BookFiles
{
    public class ContributorEvidenceResource
    {
        public int Id { get; set; }
        public int? BookFileId { get; set; }
        public int? EditionId { get; set; }
        public string ForeignEditionId { get; set; }
        public string Role { get; set; }
        public string DisplayName { get; set; }
        public string NormalizedName { get; set; }
        public string Source { get; set; }
        public string SourceLabel { get; set; }
        public int? Confidence { get; set; }
        public string ConfidenceLabel { get; set; }
        public string RawValue { get; set; }
        public string DiscoveryUrl { get; set; }
        public string ReviewOnlyReason { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
    }

    public class ContributorEvidenceUpdateResource
    {
        public int BookFileId { get; set; }
        public string Role { get; set; }
        public string DisplayName { get; set; }
        public int? Confidence { get; set; }
        public string RawValue { get; set; }
    }

    public static class ContributorEvidenceResourceMapper
    {
        public static ContributorEvidenceResource ToResource(this ContributorEvidence model)
        {
            if (model == null)
            {
                return null;
            }

            return new ContributorEvidenceResource
            {
                Id = model.Id,
                BookFileId = model.BookFileId,
                EditionId = model.EditionId,
                ForeignEditionId = model.ForeignEditionId,
                Role = model.Role,
                DisplayName = model.DisplayName,
                NormalizedName = model.NormalizedName,
                Source = model.Source,
                SourceLabel = GetSourceLabel(model.Source),
                Confidence = model.Confidence,
                ConfidenceLabel = GetConfidenceLabel(model),
                RawValue = model.RawValue,
                DiscoveryUrl = GetDiscoveryUrl(model),
                ReviewOnlyReason = GetReviewOnlyReason(model),
                Created = model.Created,
                Updated = model.Updated
            };
        }

        private static string GetSourceLabel(string source)
        {
            return source switch
            {
                "manual" => "Manual",
                "providerMetadata" => "Provider metadata",
                "aiReview" => "AI review",
                "sttTranscript" => "STT transcript",
                _ => string.IsNullOrWhiteSpace(source) ? "Unknown source" : source
            };
        }

        private static string GetConfidenceLabel(ContributorEvidence model)
        {
            if (model.Source == "providerMetadata")
            {
                return "Provider-confirmed";
            }

            if (model.Source == "manual")
            {
                return "Manual evidence";
            }

            if (!model.Confidence.HasValue)
            {
                return "Needs review";
            }

            if (model.Confidence.Value >= 85)
            {
                return "High-confidence review";
            }

            if (model.Confidence.Value >= 60)
            {
                return "Medium-confidence review";
            }

            return "Needs review";
        }

        private static string GetDiscoveryUrl(ContributorEvidence model)
        {
            if (model.Role != "narrator" || string.IsNullOrWhiteSpace(model.DisplayName))
            {
                return null;
            }

            return $"/narrators?term={Uri.EscapeDataString(model.DisplayName)}";
        }

        private static string GetReviewOnlyReason(ContributorEvidence model)
        {
            if (model.Role != "narrator")
            {
                return null;
            }

            return "Review-only narrator evidence. Use the narrator page to find related works; this evidence does not write tags or metadata automatically.";
        }
    }
}
