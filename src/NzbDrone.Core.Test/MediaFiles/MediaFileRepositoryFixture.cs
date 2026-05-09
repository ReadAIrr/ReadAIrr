using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class MediaFileRepositoryFixture : DbTest<MediaFileRepository, BookFile>
    {
        private Author _author;
        private Book _book;
        private Edition _edition;

        [SetUp]
        public void Setup()
        {
            var meta = Builder<AuthorMetadata>.CreateNew()
                .With(a => a.Id = 0)
                .Build();
            Db.Insert(meta);

            _author = Builder<Author>.CreateNew()
                .With(a => a.AuthorMetadataId = meta.Id)
                .With(a => a.Id = 0)
                .Build();
            Db.Insert(_author);

            _book = Builder<Book>.CreateNew()
                .With(a => a.Id = 0)
                .With(a => a.AuthorMetadataId = _author.AuthorMetadataId)
                .Build();
            Db.Insert(_book);

            _edition = Builder<Edition>.CreateNew()
                .With(a => a.Id = 0)
                .With(a => a.BookId = _book.Id)
                .Build();
            Db.Insert(_edition);

            var files = Builder<BookFile>.CreateListOfSize(10)
                .All()
                .With(c => c.Id = 0)
                .With(c => c.Quality = new QualityModel(Quality.MP3))
                .TheFirst(5)
                .With(c => c.EditionId = _edition.Id)
                .TheRest()
                .With(c => c.EditionId = 0)
                .TheFirst(1)
                .With(c => c.Path = @"C:\Test\Path\Author\somefile1.flac".AsOsAgnostic())
                .TheNext(1)
                .With(c => c.Path = @"C:\Test\Path\Author\somefile2.flac".AsOsAgnostic())
                .BuildListOfNew();
            Db.InsertMany(files);
        }

        [Test]
        public void get_files_by_author()
        {
            VerifyData();
            var authorFiles = Subject.GetFilesByAuthor(_author.Id);
            VerifyEagerLoaded(authorFiles);

            authorFiles.Should().OnlyContain(c => c.Author.Value.Id == _author.Id);
        }

        [Test]
        public void get_unmapped_files()
        {
            VerifyData();
            var unmappedfiles = Subject.GetUnmappedFiles();
            VerifyUnmapped(unmappedfiles);

            unmappedfiles.Should().HaveCount(5);
        }

        [Test]
        public void get_paged_unmapped_files_should_filter_by_path_term_before_paging()
        {
            InsertUnmappedFile(@"/search/GlobalTermBook.m4b".AsOsAgnostic());

            var spec = Subject.GetUnmappedFiles(new PagingSpec<BookFile>
            {
                Page = 1,
                PageSize = 10,
                SortKey = "path",
                SortDirection = SortDirection.Ascending
            }, "GlobalTermBook");

            spec.TotalRecords.Should().Be(1);
            spec.Records.Should().ContainSingle();
            spec.Records[0].Path.Should().Contain("GlobalTermBook");
        }

        [Test]
        public void get_paged_unmapped_files_should_include_suggestion_matched_ids()
        {
            var suggestionMatchedId = InsertUnmappedFile(@"/search/SuggestionOnlyMatch.m4b".AsOsAgnostic()).Id;

            var spec = Subject.GetUnmappedFiles(
                new PagingSpec<BookFile>
                {
                    Page = 1,
                    PageSize = 10,
                    SortKey = "path",
                    SortDirection = SortDirection.Ascending
                },
                "not-in-path",
                new[] { suggestionMatchedId });

            spec.TotalRecords.Should().Be(1);
            spec.Records.Should().ContainSingle();
            spec.Records[0].Id.Should().Be(suggestionMatchedId);
        }

        [Test]
        public void get_paged_unmapped_files_should_filter_reviewed_state_before_paging()
        {
            InsertUnmappedFile(@"/reviewed/ReviewedMatch.m4b".AsOsAgnostic(), true);
            InsertUnmappedFile(@"/reviewed/NeedsReviewMatch.m4b".AsOsAgnostic(), false);

            var spec = Subject.GetUnmappedFiles(
                new PagingSpec<BookFile>
                {
                    Page = 1,
                    PageSize = 10,
                    SortKey = "path",
                    SortDirection = SortDirection.Ascending
                },
                "Match",
                null,
                "reviewed");

            spec.TotalRecords.Should().Be(1);
            spec.Records.Should().ContainSingle();
            spec.Records[0].Path.Should().Contain("ReviewedMatch");
        }

        [Test]
        public void get_paged_unmapped_files_should_filter_needs_review_state_before_paging()
        {
            InsertUnmappedFile(@"/reviewed/ReviewedMatch.m4b".AsOsAgnostic(), true);
            InsertUnmappedFile(@"/reviewed/NeedsReviewMatch.m4b".AsOsAgnostic(), false);

            var spec = Subject.GetUnmappedFiles(
                new PagingSpec<BookFile>
                {
                    Page = 1,
                    PageSize = 10,
                    SortKey = "path",
                    SortDirection = SortDirection.Ascending
                },
                "Match",
                null,
                "needsReview");

            spec.TotalRecords.Should().Be(1);
            spec.Records.Should().ContainSingle();
            spec.Records[0].Path.Should().Contain("NeedsReviewMatch");
        }

        [Test]
        public void get_paged_unmapped_files_should_filter_snapshot_backed_reason_ids_before_paging()
        {
            var lowConfidenceFile = InsertUnmappedFile(@"/snapshot/LowConfidenceMatch.m4b".AsOsAgnostic());
            InsertUnmappedFile(@"/snapshot/OtherMatch.m4b".AsOsAgnostic());

            var spec = Subject.GetUnmappedFiles(
                new PagingSpec<BookFile>
                {
                    Page = 1,
                    PageSize = 10,
                    SortKey = "path",
                    SortDirection = SortDirection.Ascending
                },
                "Match",
                null,
                "lowConfidence",
                new[] { lowConfidenceFile.Id });

            spec.TotalRecords.Should().Be(1);
            spec.Records.Should().ContainSingle();
            spec.Records[0].Path.Should().Contain("LowConfidenceMatch");
        }

        [Test]
        public void get_paged_unmapped_files_should_return_empty_for_snapshot_backed_reason_without_fresh_ids()
        {
            InsertUnmappedFile(@"/snapshot/LowConfidenceMatch.m4b".AsOsAgnostic());

            var spec = Subject.GetUnmappedFiles(
                new PagingSpec<BookFile>
                {
                    Page = 1,
                    PageSize = 10,
                    SortKey = "path",
                    SortDirection = SortDirection.Ascending
                },
                "Match",
                null,
                "lowConfidence",
                new List<int>());

            spec.TotalRecords.Should().Be(0);
            spec.Records.Should().BeEmpty();
        }

        private BookFile InsertUnmappedFile(string path, bool reviewed = false)
        {
            var file = Builder<BookFile>.CreateNew()
                .With(c => c.Id = 0)
                .With(c => c.Quality = new QualityModel(Quality.MP3))
                .With(c => c.EditionId = 0)
                .With(c => c.Path = path)
                .With(c => c.Reviewed = reviewed)
                .Build();

            return Db.Insert(file);
        }

        [TestCase("C:\\Test\\Path")]
        [TestCase("C:\\Test\\Path\\")]
        public void get_files_by_base_path_should_cope_with_trailing_slash(string dir)
        {
            VerifyData();
            var firstReleaseFiles = Subject.GetFilesWithBasePath(dir.AsOsAgnostic());

            firstReleaseFiles.Should().HaveCount(2);
        }

        [TestCase("C:\\Test\\Path")]
        [TestCase("C:\\Test\\Path\\")]
        public void get_files_by_base_path_should_not_get_files_for_partial_path(string dir)
        {
            VerifyData();

            var files = Builder<BookFile>.CreateListOfSize(2)
                .All()
                .With(c => c.Id = 0)
                .With(c => c.Quality = new QualityModel(Quality.MP3))
                .TheFirst(1)
                .With(c => c.Path = @"C:\Test\Path2\Author\somefile1.flac".AsOsAgnostic())
                .TheNext(1)
                .With(c => c.Path = @"C:\Test\Path2\Author\somefile2.flac".AsOsAgnostic())
                .BuildListOfNew();
            Db.InsertMany(files);

            var firstReleaseFiles = Subject.GetFilesWithBasePath(dir.AsOsAgnostic());
            firstReleaseFiles.Should().HaveCount(2);
        }

        [Test]
        public void get_file_by_path()
        {
            VerifyData();
            var file = Subject.GetFileWithPath(@"C:\Test\Path\Author\somefile2.flac".AsOsAgnostic());

            file.Should().NotBeNull();
            file.Edition.IsLoaded.Should().BeTrue();
            file.Edition.Value.Should().NotBeNull();
            file.Author.IsLoaded.Should().BeTrue();
            file.Author.Value.Should().NotBeNull();
        }

        [Test]
        public void get_files_by_book()
        {
            VerifyData();
            var files = Subject.GetFilesByBook(_book.Id);
            VerifyEagerLoaded(files);

            files.Should().OnlyContain(c => c.EditionId == _book.Id);
        }

        private void VerifyData()
        {
            Db.All<Author>().Should().HaveCount(1);
            Db.All<Book>().Should().HaveCount(1);
            Db.All<BookFile>().Should().HaveCount(10);
        }

        private void VerifyEagerLoaded(List<BookFile> files)
        {
            foreach (var file in files)
            {
                file.Edition.IsLoaded.Should().BeTrue();
                file.Edition.Value.Should().NotBeNull();
                file.Author.IsLoaded.Should().BeTrue();
                file.Author.Value.Should().NotBeNull();
                file.Author.Value.Metadata.IsLoaded.Should().BeTrue();
                file.Author.Value.Metadata.Value.Should().NotBeNull();
            }
        }

        private void VerifyUnmapped(List<BookFile> files)
        {
            foreach (var file in files)
            {
                file.Edition.IsLoaded.Should().BeFalse();
                file.Edition.Value.Should().BeNull();
                file.Author.IsLoaded.Should().BeFalse();
                file.Author.Value.Should().BeNull();
            }
        }

        [Ignore("Doesn't make sense now we link to edition")]
        [Test]
        public void delete_files_by_book_should_work_if_join_fails()
        {
            Db.Delete(_book);
            Subject.DeleteFilesByBook(_book.Id);

            Db.All<BookFile>().Where(x => x.EditionId == _book.Id).Should().HaveCount(0);
        }
    }
}
