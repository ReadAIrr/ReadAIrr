using System;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class UnmappedFileReviewStatusRepositoryFixture : DbTest<UnmappedFileReviewStatusRepository, UnmappedFileReviewStatus>
    {
        [Test]
        public void get_fresh_book_file_ids_should_match_current_file_state()
        {
            var file = InsertUnmappedFile(@"/review/LowConfidence.m4b".AsOsAgnostic(), 100, DateTime.UtcNow);
            Subject.UpsertMany(new[]
            {
                BuildStatus(file, "lowConfidence")
            });

            var ids = Subject.GetFreshBookFileIdsMatchingStatus("lowConfidence");

            ids.Should().Equal(file.Id);
        }

        [Test]
        public void get_fresh_book_file_ids_should_ignore_stale_file_state()
        {
            var file = InsertUnmappedFile(@"/review/LowConfidence.m4b".AsOsAgnostic(), 100, DateTime.UtcNow);
            var stale = BuildStatus(file, "lowConfidence");
            stale.Size = 99;
            Subject.UpsertMany(new[] { stale });

            var ids = Subject.GetFreshBookFileIdsMatchingStatus("lowConfidence");

            ids.Should().BeEmpty();
        }

        [Test]
        public void upsert_many_should_replace_existing_status_for_book_file()
        {
            var file = InsertUnmappedFile(@"/review/StatusChange.m4b".AsOsAgnostic(), 100, DateTime.UtcNow);
            Subject.UpsertMany(new[] { BuildStatus(file, "noCandidate") });
            Subject.UpsertMany(new[] { BuildStatus(file, "metadataMismatch") });

            Subject.All().Should().ContainSingle();
            Subject.GetFreshBookFileIdsMatchingStatus("noCandidate").Should().BeEmpty();
            Subject.GetFreshBookFileIdsMatchingStatus("metadataMismatch").Should().Equal(file.Id);
        }

        private BookFile InsertUnmappedFile(string path, long size, DateTime modified)
        {
            var file = Builder<BookFile>.CreateNew()
                .With(c => c.Id = 0)
                .With(c => c.Quality = new QualityModel(Quality.MP3))
                .With(c => c.EditionId = 0)
                .With(c => c.Path = path)
                .With(c => c.Size = size)
                .With(c => c.Modified = modified)
                .Build();

            return Db.Insert(file);
        }

        private static UnmappedFileReviewStatus BuildStatus(BookFile file, string status)
        {
            return new UnmappedFileReviewStatus
            {
                BookFileId = file.Id,
                Path = file.Path,
                Size = file.Size,
                Modified = file.Modified,
                Status = status,
                ReasonKinds = status,
                Confidence = status == "lowConfidence" ? 50 : null,
                Source = "readarr-import-identification"
            };
        }
    }
}
