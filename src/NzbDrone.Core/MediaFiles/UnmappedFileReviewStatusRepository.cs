using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaFiles
{
    public interface IUnmappedFileReviewStatusRepository : IBasicRepository<UnmappedFileReviewStatus>
    {
        List<int> GetFreshBookFileIdsMatchingStatus(string status);
        void UpsertMany(IEnumerable<UnmappedFileReviewStatus> statuses);
    }

    public class UnmappedFileReviewStatusRepository : BasicRepository<UnmappedFileReviewStatus>, IUnmappedFileReviewStatusRepository
    {
        public UnmappedFileReviewStatusRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<int> GetFreshBookFileIdsMatchingStatus(string status)
        {
            if (status.IsNullOrWhiteSpace())
            {
                return new List<int>();
            }

            using (var conn = _database.OpenConnection())
            {
                return conn.Query<int>(
                    @"SELECT s.""BookFileId""
                      FROM ""UnmappedFileReviewStatuses"" s
                      JOIN ""BookFiles"" f ON f.""Id"" = s.""BookFileId""
                      WHERE f.""EditionId"" = 0
                        AND s.""Status"" = @status
                        AND s.""Path"" = f.""Path""
                        AND s.""Size"" = f.""Size""
                        AND s.""Modified"" = f.""Modified""",
                    new { status = status.Trim() }).Distinct().ToList();
            }
        }

        public void UpsertMany(IEnumerable<UnmappedFileReviewStatus> statuses)
        {
            var statusList = statuses.ToList();

            if (!statusList.Any())
            {
                return;
            }

            var bookFileIds = statusList.Select(x => x.BookFileId).Distinct().ToList();
            var existing = Query(x => bookFileIds.Contains(x.BookFileId))
                .ToDictionary(x => x.BookFileId);
            var now = DateTime.UtcNow;

            foreach (var status in statusList)
            {
                if (existing.TryGetValue(status.BookFileId, out var existingStatus))
                {
                    existingStatus.Path = status.Path;
                    existingStatus.Size = status.Size;
                    existingStatus.Modified = status.Modified;
                    existingStatus.Status = status.Status;
                    existingStatus.ReasonKinds = status.ReasonKinds;
                    existingStatus.Confidence = status.Confidence;
                    existingStatus.Source = status.Source;
                    existingStatus.Updated = now;
                    Update(existingStatus);
                }
                else
                {
                    status.Created = now;
                    status.Updated = now;
                    Insert(status);
                }
            }
        }
    }
}
