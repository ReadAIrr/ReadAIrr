using System.Collections.Generic;
using FluentAssertions;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.BookImport.Manual;
using NzbDrone.Core.Qualities;
using NzbDrone.Test.Common;
using Readarr.Api.V1.ManualImport;

namespace NzbDrone.Api.Test.ManualImport
{
    [TestFixture]
    public class ManualImportReviewSessionCacheFixture : TestBase
    {
        private ManualImportReviewSessionCache _subject;

        [SetUp]
        public void SetUp()
        {
            _subject = new ManualImportReviewSessionCache(LogManager.GetCurrentClassLogger());
        }

        [Test]
        public void manual_import_page_should_return_cached_result_for_same_scope()
        {
            var calls = 0;

            var first = _subject.GetManualImportPage("/downloads/books", null, null, FilterFilesType.Matched, true, 1, 25, false, () =>
            {
                calls++;
                return PageWithPath("/downloads/books/a.mp3");
            });

            var second = _subject.GetManualImportPage("/downloads/books", null, null, FilterFilesType.Matched, true, 1, 25, false, () =>
            {
                calls++;
                return PageWithPath("/downloads/books/b.mp3");
            });

            calls.Should().Be(1);
            first.FromCache.Should().BeFalse();
            second.FromCache.Should().BeTrue();
            second.SessionKey.Should().Be(first.SessionKey);
            second.Value.Records.Should().ContainSingle(x => x.Path == "/downloads/books/a.mp3");
            second.ExpiresAt.Should().BeAfter(second.CreatedAt);
        }

        [Test]
        public void refresh_should_bypass_cached_manual_import_page()
        {
            var calls = 0;

            _subject.GetManualImportPage("/downloads/books", null, null, FilterFilesType.Matched, true, 1, 25, false, () =>
            {
                calls++;
                return PageWithPath("/downloads/books/a.mp3");
            });

            var refreshed = _subject.GetManualImportPage("/downloads/books", null, null, FilterFilesType.Matched, true, 1, 25, true, () =>
            {
                calls++;
                return PageWithPath("/downloads/books/b.mp3");
            });

            calls.Should().Be(2);
            refreshed.FromCache.Should().BeFalse();
            refreshed.Value.Records.Should().ContainSingle(x => x.Path == "/downloads/books/b.mp3");
        }

        [Test]
        public void unmapped_review_items_should_key_by_file_state()
        {
            var calls = 0;
            var files = new List<BookFile>
            {
                new BookFile
                {
                    Id = 10,
                    Path = "/books/unmapped/a.m4b",
                    Size = 100,
                    Modified = new global::System.DateTime(2026, 5, 9, 20, 0, 0, global::System.DateTimeKind.Utc)
                }
            };

            _subject.GetUnmappedReviewItems(files, false, false, () =>
            {
                calls++;
                return ItemsWithPath("/books/unmapped/a.m4b");
            });

            files[0].Size = 200;

            var changed = _subject.GetUnmappedReviewItems(files, false, false, () =>
            {
                calls++;
                return ItemsWithPath("/books/unmapped/a-updated.m4b");
            });

            calls.Should().Be(2);
            changed.FromCache.Should().BeFalse();
            changed.Value.Should().ContainSingle(x => x.Path == "/books/unmapped/a-updated.m4b");
        }

        private static ManualImportPageResult PageWithPath(string path)
        {
            return new ManualImportPageResult
            {
                Page = 1,
                PageSize = 25,
                TotalRecords = 1,
                Records = ItemsWithPath(path)
            };
        }

        private static List<ManualImportItem> ItemsWithPath(string path)
        {
            return new List<ManualImportItem>
            {
                new ManualImportItem
                {
                    Path = path,
                    Quality = new QualityModel(Quality.MP3)
                }
            };
        }
    }
}
