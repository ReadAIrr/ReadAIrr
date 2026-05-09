using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class MultiFileBookFileCompletenessServiceFixture
    {
        private MultiFileBookFileCompletenessService _subject;

        [SetUp]
        public void SetUp()
        {
            _subject = new MultiFileBookFileCompletenessService();
        }

        [Test]
        public void should_not_flag_complete_contiguous_parts()
        {
            var files = new List<BookFile>
            {
                AudioFile("part01.mp3", 1),
                AudioFile("part02.mp3", 2),
                AudioFile("part03.mp3", 3)
            };

            _subject.GetIssue(files).Should().BeNull();
        }

        [Test]
        public void should_flag_missing_part_from_persisted_part_numbers()
        {
            var files = new List<BookFile>
            {
                AudioFile("part01.mp3", 1),
                AudioFile("part03.mp3", 3)
            };

            var issue = _subject.GetIssue(files);

            issue.Should().NotBeNull();
            issue.MissingParts.Should().Equal(2);
            issue.Message.Should().Contain("expected 3 parts");
        }

        [Test]
        public void should_flag_missing_declared_filename_total()
        {
            var files = new List<BookFile>
            {
                AudioFile("book 01 of 04.mp3", 1),
                AudioFile("book 02 of 04.mp3", 2),
                AudioFile("book 04 of 04.mp3", 4)
            };

            var issue = _subject.GetIssue(files);

            issue.Should().NotBeNull();
            issue.MissingParts.Should().Equal(3);
            issue.Message.Should().Contain("expected 4 parts");
        }

        [Test]
        public void should_flag_duplicate_parts()
        {
            var files = new List<BookFile>
            {
                AudioFile("part01.mp3", 1),
                AudioFile("part01-copy.mp3", 1),
                AudioFile("part02.mp3", 2)
            };

            var issue = _subject.GetIssue(files);

            issue.Should().NotBeNull();
            issue.Message.Should().Contain("Duplicate audiobook part numbers");
        }

        [Test]
        public void should_flag_inconsistent_filename_totals()
        {
            var files = new List<BookFile>
            {
                AudioFile("book 01 of 10.mp3", 1),
                AudioFile("book 02 of 12.mp3", 2)
            };

            var issue = _subject.GetIssue(files);

            issue.Should().NotBeNull();
            issue.Message.Should().Contain("Inconsistent part totals");
        }

        [Test]
        public void should_ignore_single_file_audiobook()
        {
            var files = new List<BookFile>
            {
                AudioFile("book.mp3", 1)
            };

            _subject.GetIssue(files).Should().BeNull();
        }

        private static BookFile AudioFile(string path, int part)
        {
            return new BookFile
            {
                Path = "/books/" + path,
                Part = part
            };
        }
    }
}
