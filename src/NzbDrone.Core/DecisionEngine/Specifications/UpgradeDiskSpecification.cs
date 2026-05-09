using System.Linq;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class UpgradeDiskSpecification : IDecisionEngineSpecification
    {
        private readonly UpgradableSpecification _upgradableSpecification;
        private readonly IAudiobookPreferenceService _audiobookPreferenceService;
        private readonly ICustomFormatCalculationService _formatService;
        private readonly Logger _logger;

        public UpgradeDiskSpecification(UpgradableSpecification qualityUpgradableSpecification,
                                        ICacheManager cacheManager,
                                        IAudiobookPreferenceService audiobookPreferenceService,
                                        ICustomFormatCalculationService formatService,
                                        Logger logger)
        {
            _upgradableSpecification = qualityUpgradableSpecification;
            _audiobookPreferenceService = audiobookPreferenceService;
            _formatService = formatService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual Decision IsSatisfiedBy(RemoteBook subject, SearchCriteriaBase searchCriteria)
        {
            var existingFiles = subject.Books.SelectMany(c => c.BookFiles.Value).ToList();
            var audiobookPreferenceCompare = _audiobookPreferenceService.Compare(subject.Author.QualityProfile.Value, existingFiles, subject);
            var hasQualityOrCustomFormatUpgrade = false;

            foreach (var file in existingFiles)
            {
                if (file == null)
                {
                    return Decision.Accept();
                }

                var customFormats = _formatService.ParseCustomFormat(file);
                var qualityCompare = new QualityModelComparer(subject.Author.QualityProfile.Value).Compare(subject.ParsedBookInfo.Quality, file.Quality);
                var currentFormatScore = subject.Author.QualityProfile.Value.CalculateCustomFormatScore(customFormats);
                var newFormatScore = subject.Author.QualityProfile.Value.CalculateCustomFormatScore(subject.CustomFormats);

                if (qualityCompare > 0 || newFormatScore > currentFormatScore)
                {
                    hasQualityOrCustomFormatUpgrade = true;
                }

                if (!_upgradableSpecification.IsUpgradable(subject.Author.QualityProfile,
                                                           file.Quality,
                                                           customFormats,
                                                           subject.ParsedBookInfo.Quality,
                                                           subject.CustomFormats))
                {
                    if (subject.Author.QualityProfile.Value.UpgradeAllowed &&
                        qualityCompare == 0 &&
                        newFormatScore == currentFormatScore &&
                        audiobookPreferenceCompare > 0)
                    {
                        _logger.Debug("New item improves configured audiobook layout/format/file-count preference");
                        continue;
                    }

                    return Decision.Reject("Existing files on disk is of equal or higher preference: {0}", file.Quality.Quality.Name);
                }
            }

            if (!hasQualityOrCustomFormatUpgrade && audiobookPreferenceCompare < 0)
            {
                return Decision.Reject("Existing files on disk better match configured audiobook layout/format/file-count preference");
            }

            return Decision.Accept();
        }
    }
}
