using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.BookImport;
using NzbDrone.Core.MediaFiles.BookImport.Manual;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class ManualImportServicePagingFixture : CoreTest<ManualImportService>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IMakeImportDecision>()
                .Setup(x => x.GetImportDecisions(It.IsAny<List<IFileInfo>>(),
                    It.IsAny<IdentificationOverrides>(),
                    It.IsAny<ImportDecisionMakerInfo>(),
                    It.IsAny<ImportDecisionMakerConfig>()))
                .Returns((List<IFileInfo> files,
                    IdentificationOverrides overrides,
                    ImportDecisionMakerInfo itemInfo,
                    ImportDecisionMakerConfig config) => files.Select(file =>
                    new ImportDecision<LocalBook>(new LocalBook
                    {
                        Path = file.FullName,
                        Quality = new QualityModel(Quality.MP3)
                    })).ToList());
        }

        [Test]
        public void paged_selected_files_should_clamp_page_and_cap_page_size()
        {
            GivenSelectedFiles("/books/a.mp3", "/books/b.mp3", "/books/c.mp3");

            var result = Subject.GetMediaFilesPage(
                new List<string>
                {
                    "/books/a.mp3",
                    "/books/b.mp3",
                    "/books/c.mp3"
                },
                null,
                false,
                0,
                ManualImportService.MaxReviewPageSize + 50);

            result.Page.Should().Be(1);
            result.PageSize.Should().Be(ManualImportService.MaxReviewPageSize);
            result.TotalRecords.Should().Be(3);
            result.Records.Select(x => x.Path).Should().Equal("/books/a.mp3", "/books/b.mp3", "/books/c.mp3");
        }

        [Test]
        public void paged_selected_files_should_return_requested_page_records_and_total_count()
        {
            GivenSelectedFiles("/books/a.mp3", "/books/b.mp3", "/books/c.mp3", "/books/d.mp3", "/books/e.mp3");

            var result = Subject.GetMediaFilesPage(
                new List<string>
                {
                    "/books/a.mp3",
                    "/books/b.mp3",
                    "/books/c.mp3",
                    "/books/d.mp3",
                    "/books/e.mp3"
                },
                null,
                false,
                2,
                2);

            result.Page.Should().Be(2);
            result.PageSize.Should().Be(2);
            result.TotalRecords.Should().Be(5);
            result.Records.Select(x => x.Path).Should().Equal("/books/c.mp3", "/books/d.mp3");
        }

        [Test]
        public void paged_folder_scan_should_return_empty_page_for_missing_folder_or_file()
        {
            Mocker.GetMock<IDiskProvider>().Setup(x => x.FolderExists("/missing")).Returns(false);
            Mocker.GetMock<IDiskProvider>().Setup(x => x.FileExists("/missing")).Returns(false);

            var result = Subject.GetMediaFilesPage("/missing", null, null, FilterFilesType.Matched, false, 1, 25);

            result.Page.Should().Be(1);
            result.PageSize.Should().Be(25);
            result.TotalRecords.Should().Be(0);
            result.Records.Should().BeEmpty();
        }

        [Test]
        public void full_selected_file_path_should_still_return_all_records()
        {
            GivenSelectedFiles("/books/a.mp3", "/books/b.mp3", "/books/c.mp3");

            var result = Subject.GetMediaFiles(
                new List<string>
                {
                    "/books/a.mp3",
                    "/books/b.mp3",
                    "/books/c.mp3"
                },
                null,
                false);

            result.Select(x => x.Path).Should().Equal("/books/a.mp3", "/books/b.mp3", "/books/c.mp3");
        }

        private void GivenSelectedFiles(params string[] paths)
        {
            foreach (var path in paths)
            {
                var file = new Mock<IFileInfo>();
                file.SetupGet(x => x.FullName).Returns(path);

                Mocker.GetMock<IDiskProvider>().Setup(x => x.FileExists(path)).Returns(true);
                Mocker.GetMock<IDiskProvider>().Setup(x => x.GetFileInfo(path)).Returns(file.Object);
                Mocker.GetMock<IDiskProvider>().Setup(x => x.GetFileSize(path)).Returns(1024);
            }
        }
    }
}
