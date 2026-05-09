using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class CutoffSpecification : IDecisionEngineSpecification
    {
        private readonly UpgradableSpecification _upgradableSpecification;
        private readonly IMultiFileBookFileCompletenessService _multiFileBookFileCompletenessService;
        private readonly Logger _logger;
        private readonly ICustomFormatCalculationService _formatService;

        public CutoffSpecification(UpgradableSpecification upgradableSpecification,
                                   IMultiFileBookFileCompletenessService multiFileBookFileCompletenessService,
                                   ICustomFormatCalculationService formatService,
                                   Logger logger)
        {
            _upgradableSpecification = upgradableSpecification;
            _multiFileBookFileCompletenessService = multiFileBookFileCompletenessService;
            _formatService = formatService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual Decision IsSatisfiedBy(RemoteBook subject, SearchCriteriaBase searchCriteria)
        {
            var qualityProfile = subject.Author.QualityProfile.Value;
            var existingFiles = subject.Books.SelectMany(b => b.BookFiles.Value).Where(x => x != null).ToList();
            var completenessIssue = _multiFileBookFileCompletenessService.GetIssue(existingFiles);

            if (completenessIssue != null)
            {
                _logger.Debug("Existing audiobook file set needs remediation: {0}", completenessIssue.Message);
                return Decision.Accept();
            }

            foreach (var file in existingFiles)
            {
                // Get a distinct list of all current track qualities for a given book
                var currentQualities = new List<QualityModel> { file.Quality };

                _logger.Debug("Comparing file quality with report. Existing files contain {0}", currentQualities.ConcatToString());

                var customFormats = _formatService.ParseCustomFormat(file);

                if (!_upgradableSpecification.CutoffNotMet(qualityProfile,
                                                           currentQualities,
                                                           customFormats,
                                                           subject.ParsedBookInfo.Quality))
                {
                    _logger.Debug("Cutoff already met by existing files, rejecting.");

                    var qualityCutoffIndex = qualityProfile.GetIndex(qualityProfile.Cutoff);
                    var qualityCutoff = qualityProfile.Items[qualityCutoffIndex.Index];

                    return Decision.Reject("Existing files meets cutoff: {0}", qualityCutoff);
                }
            }

            return Decision.Accept();
        }
    }
}
