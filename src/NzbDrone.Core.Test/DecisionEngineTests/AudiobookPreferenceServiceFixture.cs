using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class AudiobookPreferenceServiceFixture : CoreTest<AudiobookPreferenceService>
    {
        private QualityProfile _profile;

        [SetUp]
        public void Setup()
        {
            _profile = new QualityProfile
            {
                UpgradeAllowed = true,
                Cutoff = Quality.MP3.Id,
                Items = Qualities.QualityFixture.GetDefaultQualities(),
                FormatItems = new List<ProfileFormatItem>()
            };
        }

        [Test]
        public void should_be_noop_by_default()
        {
            var currentFiles = new List<BookFile>
            {
                new BookFile { Path = "/books/current/part01.mp3", Quality = new QualityModel(Quality.MP3) },
                new BookFile { Path = "/books/current/part02.mp3", Quality = new QualityModel(Quality.MP3) }
            };

            var candidate = GivenRemoteBook("Author.Book.M4B.SingleFile", Quality.M4B);

            Subject.Compare(_profile, currentFiles, candidate).Should().Be(0);
        }

        [Test]
        public void should_prefer_single_file_when_configured()
        {
            _profile.AudiobookLayoutPreference = AudiobookLayoutPreference.PreferSingleFile;

            var currentFiles = new List<BookFile>
            {
                new BookFile { Path = "/books/current/part01.mp3", Quality = new QualityModel(Quality.MP3) },
                new BookFile { Path = "/books/current/part02.mp3", Quality = new QualityModel(Quality.MP3) }
            };

            var candidate = GivenRemoteBook("Author Book unabridged m4b", Quality.UnknownAudio);

            Subject.Compare(_profile, currentFiles, candidate).Should().BePositive();
        }

        [Test]
        public void should_treat_unknown_release_layout_as_neutral()
        {
            _profile.AudiobookLayoutPreference = AudiobookLayoutPreference.PreferSingleFile;

            var currentFiles = new List<BookFile>
            {
                new BookFile { Path = "/books/current/book.mp3", Quality = new QualityModel(Quality.MP3) }
            };

            var candidate = GivenRemoteBook("Author Book Audiobook", Quality.UnknownAudio);

            Subject.Compare(_profile, currentFiles, candidate).Should().Be(0);
        }

        [Test]
        public void should_prefer_fewer_parts_when_counts_are_known()
        {
            _profile.AudiobookFileCountPreference = AudiobookFileCountPreference.PreferFewerParts;

            var currentFiles = new List<BookFile>
            {
                new BookFile { Path = "/books/current/part01.mp3", Quality = new QualityModel(Quality.MP3) },
                new BookFile { Path = "/books/current/part02.mp3", Quality = new QualityModel(Quality.MP3) },
                new BookFile { Path = "/books/current/part03.mp3", Quality = new QualityModel(Quality.MP3) }
            };

            var candidate = GivenRemoteBook("Author Book 1 file m4b", Quality.UnknownAudio);

            Subject.Compare(_profile, currentFiles, candidate).Should().BePositive();
        }

        [Test]
        public void should_prefer_mp3_when_configured_and_format_is_known()
        {
            _profile.AudiobookFormatPreference = AudiobookFormatPreference.PreferMP3;

            var currentFiles = new List<BookFile>
            {
                new BookFile { Path = "/books/current/book.m4b", Quality = new QualityModel(Quality.M4B) }
            };

            var candidate = GivenRemoteBook("Author Book mp3 12 files", Quality.UnknownAudio);

            Subject.Compare(_profile, currentFiles, candidate).Should().BePositive();
        }

        private static RemoteBook GivenRemoteBook(string releaseTitle, Quality quality)
        {
            return new RemoteBook
            {
                Release = new ReleaseInfo { Title = releaseTitle },
                ParsedBookInfo = new ParsedBookInfo
                {
                    ReleaseTitle = releaseTitle,
                    Quality = new QualityModel(quality)
                }
            };
        }
    }
}
