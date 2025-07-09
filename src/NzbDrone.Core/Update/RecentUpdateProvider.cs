using System;
using System.Collections.Generic;
using NLog;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Update.History;

namespace NzbDrone.Core.Update
{
    public interface IRecentUpdateProvider
    {
        List<UpdatePackage> GetRecentUpdatePackages();
    }

    public class RecentUpdateProvider : IRecentUpdateProvider
    {
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IUpdatePackageProvider _updatePackageProvider;
        private readonly IUpdateHistoryService _updateHistoryService;
        private readonly Logger _logger;

        public RecentUpdateProvider(IConfigFileProvider configFileProvider,
                                    IUpdatePackageProvider updatePackageProvider,
                                    IUpdateHistoryService updateHistoryService,
                                    Logger logger)
        {
            _configFileProvider = configFileProvider;
            _updatePackageProvider = updatePackageProvider;
            _updateHistoryService = updateHistoryService;
            _logger = logger;
        }

        public List<UpdatePackage> GetRecentUpdatePackages()
        {
            try
            {
                var branch = _configFileProvider.Branch;
                var version = BuildInfo.Version;
                var prevVersion = _updateHistoryService.PreviouslyInstalled();
                return _updatePackageProvider.GetRecentUpdates(branch, version, prevVersion);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to fetch recent updates from ReadAIrr update service. This is expected for new ReadAIrr installations until the update service is available.");

                // Return empty list for now - ReadAIrr update service is not yet available
                return new List<UpdatePackage>();
            }
        }
    }
}
