using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NzbDrone.Core.MediaFiles
{
    public interface IMultiFileBookFileCompletenessService
    {
        MultiFileBookFileCompletenessIssue GetIssue(List<BookFile> files);
    }

    public class MultiFileBookFileCompletenessService : IMultiFileBookFileCompletenessService
    {
        private static readonly Regex PartTotalRegex = new Regex(@"(?:^|[^\d])(?<part>\d{1,4})\s*(?:of|/)\s*(?<total>\d{1,4})(?:[^\d]|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public MultiFileBookFileCompletenessIssue GetIssue(List<BookFile> files)
        {
            var audioFiles = files
                .Where(x => MediaFileExtensions.AudioExtensions.Contains(Path.GetExtension(x.Path)))
                .ToList();

            if (audioFiles.Count <= 1)
            {
                return null;
            }

            var declaredParts = audioFiles
                .Select(x => new
                {
                    File = x,
                    Parsed = ParseDeclaredPartTotal(x.Path)
                })
                .ToList();

            var totals = declaredParts
                .Where(x => x.Parsed != null)
                .Select(x => x.Parsed.Total)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            if (totals.Count > 1)
            {
                return MultiFileBookFileCompletenessIssue.FromMessage(
                    $"Inconsistent part totals found in filenames: {string.Join(", ", totals)}.");
            }

            var declaredPartNumbers = declaredParts
                .Where(x => x.Parsed != null)
                .Select(x => x.Parsed.Part)
                .ToList();

            var partNumbers = declaredPartNumbers.Any() ? declaredPartNumbers : audioFiles.Select(x => x.Part).Where(x => x > 0).ToList();

            if (!partNumbers.Any())
            {
                return null;
            }

            var duplicateParts = partNumbers
                .GroupBy(x => x)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .OrderBy(x => x)
                .ToList();

            if (duplicateParts.Any())
            {
                return MultiFileBookFileCompletenessIssue.FromMessage(
                    $"Duplicate audiobook part numbers found: {string.Join(", ", duplicateParts)}.");
            }

            var expectedTotal = totals.SingleOrDefault();
            var expectedMax = expectedTotal > 0 ? expectedTotal : partNumbers.Max();
            var missingParts = Enumerable.Range(1, expectedMax)
                .Except(partNumbers)
                .ToList();

            if (expectedTotal > 0 && audioFiles.Count < expectedTotal)
            {
                return MultiFileBookFileCompletenessIssue.FromMissingParts(expectedTotal, missingParts);
            }

            if (missingParts.Any())
            {
                return MultiFileBookFileCompletenessIssue.FromMissingParts(expectedMax, missingParts);
            }

            return null;
        }

        private static DeclaredPartTotal ParseDeclaredPartTotal(string path)
        {
            var filename = Path.GetFileNameWithoutExtension(path);
            var match = PartTotalRegex.Match(filename);

            if (!match.Success)
            {
                return null;
            }

            var part = int.Parse(match.Groups["part"].Value);
            var total = int.Parse(match.Groups["total"].Value);

            if (part <= 0 || total <= 0 || part > total)
            {
                return null;
            }

            return new DeclaredPartTotal(part, total);
        }

        private sealed class DeclaredPartTotal
        {
            public DeclaredPartTotal(int part, int total)
            {
                Part = part;
                Total = total;
            }

            public int Part { get; }
            public int Total { get; }
        }
    }

    public class MultiFileBookFileCompletenessIssue
    {
        public string Message { get; set; }
        public List<int> MissingParts { get; set; } = new List<int>();

        public static MultiFileBookFileCompletenessIssue FromMessage(string message)
        {
            return new MultiFileBookFileCompletenessIssue
            {
                Message = message
            };
        }

        public static MultiFileBookFileCompletenessIssue FromMissingParts(int expectedTotal, List<int> missingParts)
        {
            var missingText = missingParts.Any() ? string.Join(", ", missingParts) : "unknown";

            return new MultiFileBookFileCompletenessIssue
            {
                MissingParts = missingParts,
                Message = $"Incomplete audiobook part set: expected {expectedTotal} parts, missing {missingText}."
            };
        }
    }
}
