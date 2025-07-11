﻿using System;
using System.IO;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Processes;
using IServiceProvider = NzbDrone.Common.IServiceProvider;

namespace NzbDrone.Update.UpdateEngine
{
    public interface IStartNzbDrone
    {
        void Start(AppType appType, string installationFolder);
    }

    public class StartNzbDrone : IStartNzbDrone
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IProcessProvider _processProvider;
        private readonly IStartupContext _startupContext;
        private readonly Logger _logger;

        public StartNzbDrone(IServiceProvider serviceProvider, IProcessProvider processProvider, IStartupContext startupContext, Logger logger)
        {
            _serviceProvider = serviceProvider;
            _processProvider = processProvider;
            _startupContext = startupContext;
            _logger = logger;
        }

        public void Start(AppType appType, string installationFolder)
        {
            _logger.Info("Starting Readarr");
            if (appType == AppType.Service)
            {
                try
                {
                    StartService();
                }
                catch (InvalidOperationException e)
                {
                    _logger.Warn(e, "Couldn't start Readarr Service (Most likely due to permission issues). Falling back to console.");
                    StartConsole(installationFolder);
                }
            }
            else if (appType == AppType.Console)
            {
                StartConsole(installationFolder);
            }
            else
            {
                StartWinform(installationFolder);
            }
        }

        private void StartService()
        {
            _logger.Info("Starting Readarr service");
            _serviceProvider.Start(ServiceProvider.SERVICE_NAME);
        }

        private void StartWinform(string installationFolder)
        {
            Start(installationFolder, "ReadAIrr".ProcessNameToExe());
        }

        private void StartConsole(string installationFolder)
        {
            Start(installationFolder, "Readarr.Console".ProcessNameToExe());
        }

        private void Start(string installationFolder, string fileName)
        {
            _logger.Info("Starting {0}", fileName);
            var path = Path.Combine(installationFolder, fileName);

            if (!_startupContext.Flags.Contains(StartupContext.NO_BROWSER))
            {
                _startupContext.Flags.Add(StartupContext.NO_BROWSER);
            }

            _processProvider.SpawnNewProcess(path, _startupContext.PreservedArguments);
        }
    }
}
