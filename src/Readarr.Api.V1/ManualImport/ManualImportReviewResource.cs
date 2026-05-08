using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.MediaFiles.BookImport.Manual;
using NzbDrone.Core.Parser.Model;

namespace Readarr.Api.V1.ManualImport
{
    public class ManualImportReviewResource
    {
        public string Status { get; set; }
        public string StatusLabel { get; set; }
        public int? Confidence { get; set; }
        public string Provider { get; set; }
        public ManualImportParsedResource Parsed { get; set; }
        public ManualImportCandidateResource Candidate { get; set; }
        public List<ManualImportReviewReasonResource> Reasons { get; set; }
        public List<ManualImportReviewReasonResource> Hints { get; set; }
        public List<ManualImportIdentificationSuggestionResource> Suggestions { get; set; }
    }

    public class ManualImportParsedResource
    {
        public string Author { get; set; }
        public string Book { get; set; }
        public string Title { get; set; }
        public string Isbn { get; set; }
        public string Asin { get; set; }
        public int Year { get; set; }
        public string SeriesTitle { get; set; }
        public string SeriesIndex { get; set; }
    }

    public class ManualImportCandidateResource
    {
        public int? AuthorId { get; set; }
        public string AuthorName { get; set; }
        public int? BookId { get; set; }
        public string BookTitle { get; set; }
        public string ForeignEditionId { get; set; }
        public string EditionTitle { get; set; }
        public string EditionLanguage { get; set; }
        public string EditionFormat { get; set; }
        public string EditionIsbn { get; set; }
        public DateTime? EditionReleaseDate { get; set; }
    }

    public class ManualImportReviewReasonResource
    {
        public string Kind { get; set; }
        public string Label { get; set; }
        public string Detail { get; set; }
    }

    public class ManualImportIdentificationSuggestionResource
    {
        public string Type { get; set; }
        public string Provider { get; set; }
        public string Status { get; set; }
        public string Path { get; set; }
        public string LikelyAuthor { get; set; }
        public string LikelyBook { get; set; }
        public string LikelyEdition { get; set; }
        public string Language { get; set; }
        public string Narrator { get; set; }
        public int? Confidence { get; set; }
        public string Explanation { get; set; }
        public bool RequiresManualConfirmation { get; set; }
        public string TranscriptExcerpt { get; set; }
        public string ContextSummary { get; set; }
        public List<ManualImportReviewReasonResource> Evidence { get; set; }
        public List<ManualImportReviewReasonResource> Warnings { get; set; }
        public bool IsStale { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Updated { get; set; }
    }

    public static class ManualImportReviewResourceMapper
    {
        public static ManualImportReviewResource ToReviewResource(this ManualImportItem model, bool reviewed = false)
        {
            var reasons = new List<ManualImportReviewReasonResource>();
            var hints = new List<ManualImportReviewReasonResource>();
            var parsed = BuildParsed(model);
            var candidate = BuildCandidate(model);
            var confidence = model.MatchDistance.HasValue ? Math.Max(0, Math.Min(100, (int)Math.Round((1 - model.MatchDistance.Value) * 100))) : (int?)null;

            if (reviewed)
            {
                reasons.Add(new ManualImportReviewReasonResource
                {
                    Kind = "reviewed",
                    Label = "Reviewed",
                    Detail = "Marked reviewed for this triage pass."
                });
            }

            if (model.Author == null)
            {
                reasons.Add(new ManualImportReviewReasonResource
                {
                    Kind = "missingAuthor",
                    Label = "Missing author",
                    Detail = parsed.Author.IsNotNullOrWhiteSpace() ? $"Parsed author: {parsed.Author}" : "No author could be parsed or matched."
                });
            }

            if (model.Book == null)
            {
                reasons.Add(new ManualImportReviewReasonResource
                {
                    Kind = "missingBook",
                    Label = "Missing book",
                    Detail = parsed.Book.IsNotNullOrWhiteSpace() ? $"Parsed book: {parsed.Book}" : "No book could be parsed or matched."
                });
            }

            if (model.Book != null && model.Edition == null)
            {
                reasons.Add(new ManualImportReviewReasonResource
                {
                    Kind = "noEdition",
                    Label = "No edition",
                    Detail = "A book candidate was found, but no edition candidate was selected."
                });
            }

            if (model.Quality == null)
            {
                reasons.Add(new ManualImportReviewReasonResource
                {
                    Kind = "missingQuality",
                    Label = "Missing quality",
                    Detail = "No quality could be detected."
                });
            }

            foreach (var rejection in model.Rejections ?? Enumerable.Empty<Rejection>())
            {
                var kind = GetRejectionKind(rejection);

                reasons.Add(new ManualImportReviewReasonResource
                {
                    Kind = kind,
                    Label = $"{rejection.Type} rejection",
                    Detail = rejection.Reason
                });

                if (kind == "metadataMismatch")
                {
                    hints.Add(new ManualImportReviewReasonResource
                    {
                        Kind = kind,
                        Label = "Metadata mismatch",
                        Detail = rejection.Reason
                    });
                }
            }

            if (model.MatchDistanceReasons.IsNotNullOrWhiteSpace())
            {
                hints.Add(new ManualImportReviewReasonResource
                {
                    Kind = "matchDistance",
                    Label = "Match distance",
                    Detail = model.MatchDistanceReasons
                });
            }

            var status = GetStatus(reviewed, model, reasons, confidence);

            return new ManualImportReviewResource
            {
                Status = status,
                StatusLabel = GetStatusLabel(status),
                Confidence = confidence,
                Provider = "readarr-import-identification",
                Parsed = parsed,
                Candidate = candidate,
                Reasons = reasons.Take(5).ToList(),
                Hints = hints.Take(5).ToList(),
                Suggestions = new List<ManualImportIdentificationSuggestionResource>()
            };
        }

        public static void ApplySuggestionEvidence(ManualImportReviewResource review)
        {
            if (review?.Suggestions == null)
            {
                return;
            }

            foreach (var suggestion in review.Suggestions)
            {
                suggestion.Evidence = new List<ManualImportReviewReasonResource>();
                suggestion.Warnings = new List<ManualImportReviewReasonResource>();

                AddCandidateEvidence(suggestion, "book", "Title evidence", suggestion.LikelyBook, review.Candidate?.BookTitle);
                AddCandidateEvidence(suggestion, "author", "Author evidence", suggestion.LikelyAuthor, review.Candidate?.AuthorName);
                AddCandidateEvidence(suggestion, "edition", "Edition evidence", suggestion.LikelyEdition, review.Candidate?.EditionTitle);

                if (suggestion.Narrator.IsNotNullOrWhiteSpace())
                {
                    suggestion.Evidence.Add(new ManualImportReviewReasonResource
                    {
                        Kind = "narratorEvidence",
                        Label = "Narrator evidence",
                        Detail = $"{GetSuggestionSource(suggestion)} found narrator: {suggestion.Narrator}"
                    });
                }
            }

            AddNarratorConflictWarnings(review.Suggestions);
        }

        private static ManualImportParsedResource BuildParsed(ManualImportItem model)
        {
            var tags = model.Tags ?? new ParsedTrackInfo();
            var filename = Path.GetFileNameWithoutExtension(model.Path ?? model.Name ?? string.Empty);
            var parsedTitle = NzbDrone.Core.Parser.Parser.ParseBookTitle(filename);

            return new ManualImportParsedResource
            {
                Author = tags.AuthorTitle ?? parsedTitle?.AuthorName,
                Book = tags.BookTitle ?? parsedTitle?.BookTitle,
                Title = tags.Title ?? filename,
                Isbn = tags.Isbn,
                Asin = tags.Asin,
                Year = (int)tags.Year,
                SeriesTitle = tags.SeriesTitle,
                SeriesIndex = tags.SeriesIndex
            };
        }

        private static void AddCandidateEvidence(ManualImportIdentificationSuggestionResource suggestion, string kind, string label, string suggestionValue, string candidateValue)
        {
            if (suggestionValue.IsNullOrWhiteSpace())
            {
                return;
            }

            if (candidateValue.IsNullOrWhiteSpace())
            {
                suggestion.Evidence.Add(new ManualImportReviewReasonResource
                {
                    Kind = $"{kind}Evidence",
                    Label = label,
                    Detail = $"{GetSuggestionSource(suggestion)} found {kind}: {suggestionValue}"
                });

                return;
            }

            if (IsLikelySameText(suggestionValue, candidateValue))
            {
                suggestion.Evidence.Add(new ManualImportReviewReasonResource
                {
                    Kind = $"{kind}Match",
                    Label = label,
                    Detail = $"{GetSuggestionSource(suggestion)} agrees with the current candidate: {suggestionValue}"
                });

                return;
            }

            suggestion.Warnings.Add(new ManualImportReviewReasonResource
            {
                Kind = $"{kind}Mismatch",
                Label = $"{label} mismatch",
                Detail = $"{GetSuggestionSource(suggestion)} found {kind} '{suggestionValue}', but the current candidate is '{candidateValue}'."
            });
        }

        private static void AddNarratorConflictWarnings(List<ManualImportIdentificationSuggestionResource> suggestions)
        {
            var narratorSuggestions = suggestions
                .Where(x => x.Narrator.IsNotNullOrWhiteSpace())
                .ToList();

            foreach (var suggestion in narratorSuggestions)
            {
                var conflict = narratorSuggestions.FirstOrDefault(x => !ReferenceEquals(x, suggestion) && !IsLikelySameText(x.Narrator, suggestion.Narrator));

                if (conflict == null)
                {
                    continue;
                }

                suggestion.Warnings.Add(new ManualImportReviewReasonResource
                {
                    Kind = "narratorMismatch",
                    Label = "Narrator evidence mismatch",
                    Detail = $"{GetSuggestionSource(suggestion)} found narrator '{suggestion.Narrator}', but {GetSuggestionSource(conflict)} found '{conflict.Narrator}'."
                });
            }
        }

        private static string GetSuggestionSource(ManualImportIdentificationSuggestionResource suggestion)
        {
            if (suggestion.Type == "deepAudio")
            {
                return suggestion.Provider.IsNotNullOrWhiteSpace() && suggestion.Provider.Contains("stt") ? "Transcript" : "Deep identify";
            }

            return "AI review";
        }

        private static bool IsLikelySameText(string left, string right)
        {
            var normalizedLeft = NormalizeEvidenceText(left);
            var normalizedRight = NormalizeEvidenceText(right);

            if (normalizedLeft.IsNullOrWhiteSpace() || normalizedRight.IsNullOrWhiteSpace())
            {
                return false;
            }

            return normalizedLeft == normalizedRight ||
                   (normalizedLeft.Length > 6 && normalizedRight.Contains(normalizedLeft)) ||
                   (normalizedRight.Length > 6 && normalizedLeft.Contains(normalizedRight));
        }

        private static string NormalizeEvidenceText(string value)
        {
            return new string((value ?? string.Empty)
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray());
        }

        private static ManualImportCandidateResource BuildCandidate(ManualImportItem model)
        {
            var author = model.Author;
            var book = model.Book;
            var edition = model.Edition;

            return new ManualImportCandidateResource
            {
                AuthorId = author?.Id,
                AuthorName = GetAuthorName(author),
                BookId = book?.Id,
                BookTitle = book?.Title,
                ForeignEditionId = edition?.ForeignEditionId,
                EditionTitle = edition?.Title,
                EditionLanguage = edition?.Language,
                EditionFormat = edition?.Format,
                EditionIsbn = edition?.Isbn13,
                EditionReleaseDate = edition?.ReleaseDate
            };
        }

        private static string GetAuthorName(NzbDrone.Core.Books.Author author)
        {
            if (author == null)
            {
                return null;
            }

            return author.Metadata?.Value?.Name ?? author.Name;
        }

        private static string GetRejectionKind(Rejection rejection)
        {
            var reason = rejection.Reason ?? string.Empty;

            if (reason.Contains("not close enough"))
            {
                return "lowConfidence";
            }

            if (reason.Contains("language") || reason.Contains("isbn") || reason.Contains("year") || reason.Contains("edition"))
            {
                return "metadataMismatch";
            }

            return "rejection";
        }

        private static string GetStatus(bool reviewed, ManualImportItem model, List<ManualImportReviewReasonResource> reasons, int? confidence)
        {
            if (reviewed)
            {
                return "reviewed";
            }

            if (model.Author == null || model.Book == null)
            {
                return "noCandidate";
            }

            if (reasons.Any(x => x.Kind == "metadataMismatch"))
            {
                return "metadataMismatch";
            }

            if (reasons.Any(x => x.Kind == "noEdition"))
            {
                return "noEdition";
            }

            if (reasons.Any(x => x.Kind == "lowConfidence") || (confidence.HasValue && confidence.Value < 80))
            {
                return "lowConfidence";
            }

            if (reasons.Any())
            {
                return "needsReview";
            }

            return "ready";
        }

        private static string GetStatusLabel(string status)
        {
            switch (status)
            {
                case "reviewed":
                    return "Reviewed";
                case "noCandidate":
                    return "No candidate";
                case "metadataMismatch":
                    return "Metadata mismatch";
                case "noEdition":
                    return "No edition";
                case "lowConfidence":
                    return "Low confidence";
                case "ready":
                    return "Ready";
                default:
                    return "Needs review";
            }
        }
    }
}
