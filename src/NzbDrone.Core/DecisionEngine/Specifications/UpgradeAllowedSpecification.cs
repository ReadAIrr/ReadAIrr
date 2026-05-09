using System.Linq;
using NLog;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class UpgradeAllowedSpecification : IDecisionEngineSpecification
    {
        private readonly UpgradableSpecification _upgradableSpecification;
        private readonly IAudiobookPreferenceService _audiobookPreferenceService;
        private readonly ICustomFormatCalculationService _formatService;
        private readonly Logger _logger;

        public UpgradeAllowedSpecification(UpgradableSpecification upgradableSpecification,
                                           Logger logger,
                                           IAudiobookPreferenceService audiobookPreferenceService,
                                           ICustomFormatCalculationService formatService)
        {
            _upgradableSpecification = upgradableSpecification;
            _audiobookPreferenceService = audiobookPreferenceService;
            _formatService = formatService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual Decision IsSatisfiedBy(RemoteBook subject, SearchCriteriaBase searchCriteria)
        {
            var qualityProfile = subject.Author.QualityProfile.Value;
            var existingFiles = subject.Books.SelectMany(b => b.BookFiles.Value).ToList();
            var audiobookPreferenceCompare = _audiobookPreferenceService.Compare(qualityProfile, existingFiles, subject);
            var hasQualityOrCustomFormatUpgrade = false;

            foreach (var file in existingFiles)
            {
                if (file == null)
                {
                    _logger.Debug("File is no longer available, skipping this file.");
                    continue;
                }

                var fileCustomFormats = _formatService.ParseCustomFormat(file, subject.Author);
                var qualityCompare = new QualityModelComparer(qualityProfile).Compare(subject.ParsedBookInfo.Quality, file.Quality);
                var currentFormatScore = qualityProfile.CalculateCustomFormatScore(fileCustomFormats);
                var newFormatScore = qualityProfile.CalculateCustomFormatScore(subject.CustomFormats);

                if (qualityCompare > 0 || newFormatScore > currentFormatScore)
                {
                    hasQualityOrCustomFormatUpgrade = true;
                }

                _logger.Debug("Comparing file quality with report. Existing files contain {0}", file.Quality);

                if (!_upgradableSpecification.IsUpgradeAllowed(qualityProfile,
                                                               file.Quality,
                                                               fileCustomFormats,
                                                               subject.ParsedBookInfo.Quality,
                                                               subject.CustomFormats))
                {
                    if (qualityProfile.UpgradeAllowed &&
                        qualityCompare == 0 &&
                        newFormatScore == currentFormatScore &&
                        audiobookPreferenceCompare > 0)
                    {
                        _logger.Debug("Quality profile allows upgrading for configured audiobook layout/format/file-count preference");
                        continue;
                    }

                    _logger.Debug("Upgrading is not allowed by the quality profile");

                    return Decision.Reject("Existing files and the Quality profile does not allow upgrades");
                }
            }

            if (!hasQualityOrCustomFormatUpgrade && audiobookPreferenceCompare < 0)
            {
                return Decision.Reject("Existing files better match configured audiobook layout/format/file-count preference");
            }

            return Decision.Accept();
        }
    }
}
