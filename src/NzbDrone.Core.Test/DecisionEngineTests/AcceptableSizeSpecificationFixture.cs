using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class AcceptableSizeSpecificationFixture : CoreTest<AcceptableSizeSpecification>
    {
        private RemoteBook _remoteBook;
        private QualityDefinition _qualityDefinition;

        [SetUp]
        public void Setup()
        {
            _qualityDefinition = new QualityDefinition(Quality.MP3)
            {
                MinSize = 100,
                MaxSize = 200,
                EnforceSizeLimits = false
            };

            _remoteBook = new RemoteBook
            {
                Release = new ReleaseInfo
                {
                    Title = "Author Book MP3",
                    Size = 50.Kilobits()
                },
                ParsedBookInfo = new ParsedBookInfo
                {
                    Quality = new QualityModel(Quality.MP3)
                }
            };

            Mocker.GetMock<IQualityDefinitionService>()
                  .Setup(x => x.Get(Quality.MP3))
                  .Returns(_qualityDefinition);
        }

        [Test]
        public void should_accept_outside_bounds_when_size_limits_are_not_enforced()
        {
            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_below_minimum_when_size_limits_are_enforced()
        {
            _qualityDefinition.EnforceSizeLimits = true;

            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_reject_above_maximum_when_size_limits_are_enforced()
        {
            _qualityDefinition.EnforceSizeLimits = true;
            _remoteBook.Release.Size = 250.Kilobits();

            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_accept_unknown_release_size()
        {
            _qualityDefinition.EnforceSizeLimits = true;
            _remoteBook.Release.Size = 0;

            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeTrue();
        }
    }
}
