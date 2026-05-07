using System.Linq;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.IndexerSearch
{
    public class AuthorSearchService : IExecute<AuthorSearchCommand>
    {
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IProcessDownloadDecisions _processDownloadDecisions;
        private readonly IAuthorIdentityLinkService _authorIdentityLinkService;
        private readonly Logger _logger;

        public AuthorSearchService(ISearchForReleases releaseSearchService,
            IProcessDownloadDecisions processDownloadDecisions,
            IAuthorIdentityLinkService authorIdentityLinkService,
            Logger logger)
        {
            _releaseSearchService = releaseSearchService;
            _processDownloadDecisions = processDownloadDecisions;
            _authorIdentityLinkService = authorIdentityLinkService;
            _logger = logger;
        }

        public void Execute(AuthorSearchCommand message)
        {
            var authorIds = _authorIdentityLinkService.GetAuthorIdentityIds(message.AuthorId);
            var decisions = authorIds
                .SelectMany(x => _releaseSearchService.AuthorSearch(x, false, message.Trigger == CommandTrigger.Manual, false).GetAwaiter().GetResult())
                .ToList();
            var processed = _processDownloadDecisions.ProcessDecisions(decisions).GetAwaiter().GetResult();

            _logger.ProgressInfo("Author search completed. {0} reports downloaded.", processed.Grabbed.Count);
        }
    }
}
