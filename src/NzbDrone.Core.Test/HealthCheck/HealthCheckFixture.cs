using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.HealthCheck
{
    [TestFixture]
    public class HealthCheckFixture : CoreTest
    {
        private const string WikiRoot = "https://readairr.com/docs/wiki/";

        [TestCase("I blew up because of some weird user mistake", null, WikiRoot + "readarr/system.html#i-blew-up-because-of-some-weird-user-mistake")]
        [TestCase("I blew up because of some weird user mistake", "#my-health-check", WikiRoot + "readarr/system.html#my-health-check")]
        [TestCase("I blew up because of some weird user mistake", "custom-page#my-health-check", WikiRoot + "readarr/custom-page#my-health-check")]
        public void should_format_wiki_url(string message, string wikiFragment, string expectedUrl)
        {
            var subject = new NzbDrone.Core.HealthCheck.HealthCheck(typeof(HealthCheckBase), HealthCheckResult.Warning, message, wikiFragment);

            subject.WikiUrl.Should().Be(expectedUrl);
        }
    }
}
