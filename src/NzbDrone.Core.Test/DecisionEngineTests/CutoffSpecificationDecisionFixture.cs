using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class CutoffSpecificationDecisionFixture : CoreTest<CutoffSpecification>
    {
        private RemoteBook _remoteBook;

        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<IMultiFileBookFileCompletenessService>(new MultiFileBookFileCompletenessService());

            var profile = new QualityProfile
            {
                Cutoff = Quality.MP3.Id,
                Items = Qualities.QualityFixture.GetDefaultQualities(),
                UpgradeAllowed = true,
                FormatItems = new List<ProfileFormatItem>()
            };

            var author = new Author
            {
                QualityProfile = new LazyLoaded<QualityProfile>(profile)
            };

            _remoteBook = new RemoteBook
            {
                Author = author,
                Release = new ReleaseInfo { Title = "Author Book MP3" },
                ParsedBookInfo = new ParsedBookInfo { Quality = new QualityModel(Quality.MP3) },
                Books = new List<Book>
                {
                    new Book
                    {
                        BookFiles = new List<BookFile>
                        {
                            new BookFile { Path = "/books/current/Book 01 of 03.mp3", Part = 1, Quality = new QualityModel(Quality.MP3) },
                            new BookFile { Path = "/books/current/Book 03 of 03.mp3", Part = 3, Quality = new QualityModel(Quality.MP3) }
                        }
                    }
                }
            };

            Mocker.GetMock<ICustomFormatCalculationService>()
                  .Setup(x => x.ParseCustomFormat(It.IsAny<BookFile>()))
                  .Returns(new List<CustomFormat>());
        }

        [Test]
        public void should_accept_when_existing_audiobook_set_is_incomplete_even_if_cutoff_is_met()
        {
            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeTrue();
        }
    }
}
