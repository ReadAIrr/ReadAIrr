using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
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
                    Kind = "missingEdition",
                    Label = "Missing edition",
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
                Hints = hints.Take(5).ToList()
            };
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
