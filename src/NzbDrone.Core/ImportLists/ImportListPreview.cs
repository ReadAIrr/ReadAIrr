using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.ImportLists
{
    public static class ImportListPreviewStatus
    {
        public const string WouldAdd = "wouldAdd";
        public const string WouldAddAuthor = "wouldAddAuthor";
        public const string AlreadyExists = "alreadyExists";
        public const string ExcludedByMetadataProfile = "excludedByMetadataProfile";
        public const string ExcludedByImportListExclusion = "excludedByImportListExclusion";
        public const string InvalidMetadata = "invalidMetadata";
        public const string WouldMonitorExisting = "wouldMonitorExisting";
        public const string WouldSkipUnmonitoredAuthor = "wouldSkipUnmonitoredAuthor";
        public const string AlreadyQueued = "alreadyQueued";
    }

    public class ImportListPreview
    {
        public ImportListPreview()
        {
            Buckets = new List<ImportListPreviewBucket>();
            Samples = new List<ImportListPreviewItem>();
        }

        public int TotalItems { get; set; }
        public List<ImportListPreviewBucket> Buckets { get; set; }
        public List<ImportListPreviewItem> Samples { get; set; }

        public static ImportListPreview FromDecisions(List<ImportListPreviewItem> decisions, int sampleLimit = 5)
        {
            return new ImportListPreview
            {
                TotalItems = decisions.Count,
                Buckets = decisions
                    .GroupBy(x => x.Status)
                    .Select(x => new ImportListPreviewBucket
                    {
                        Status = x.Key,
                        Count = x.Count(),
                        Label = ImportListPreviewLabels.GetValueOrDefault(x.Key, x.Key)
                    })
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Label)
                    .ToList(),
                Samples = decisions
                    .GroupBy(x => x.Status)
                    .SelectMany(x => x.Take(sampleLimit))
                    .ToList()
            };
        }

        private static readonly Dictionary<string, string> ImportListPreviewLabels = new Dictionary<string, string>
        {
            { ImportListPreviewStatus.WouldAdd, "Will add" },
            { ImportListPreviewStatus.WouldAddAuthor, "Will add author" },
            { ImportListPreviewStatus.AlreadyExists, "Already exists" },
            { ImportListPreviewStatus.ExcludedByMetadataProfile, "Excluded by metadata profile" },
            { ImportListPreviewStatus.ExcludedByImportListExclusion, "Excluded by import-list exclusion" },
            { ImportListPreviewStatus.InvalidMetadata, "Invalid metadata" },
            { ImportListPreviewStatus.WouldMonitorExisting, "Will monitor existing" },
            { ImportListPreviewStatus.WouldSkipUnmonitoredAuthor, "Skipped by monitor mode" },
            { ImportListPreviewStatus.AlreadyQueued, "Already queued from this list" }
        };
    }

    public class ImportListPreviewBucket
    {
        public string Status { get; set; }
        public string Label { get; set; }
        public int Count { get; set; }
    }

    public class ImportListPreviewItem
    {
        public ImportListPreviewItem()
        {
        }

        public ImportListPreviewItem(ImportListItemInfo report, string status, string reason)
        {
            Status = status;
            Reason = reason;
            ImportList = report.ImportList;
            Author = report.Author;
            AuthorForeignId = report.AuthorGoodreadsId;
            Book = report.Book;
            BookForeignId = report.BookGoodreadsId;
            EditionForeignId = report.EditionGoodreadsId;
        }

        public string Status { get; set; }
        public string Reason { get; set; }
        public string ImportList { get; set; }
        public string Author { get; set; }
        public string AuthorForeignId { get; set; }
        public string Book { get; set; }
        public string BookForeignId { get; set; }
        public string EditionForeignId { get; set; }
    }
}
