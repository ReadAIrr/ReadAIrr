using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Internal;
using Npgsql;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Backup;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Update;
using Readarr.Http;
using Readarr.Http.Validation;

namespace Readarr.Api.V1.System
{
    [V1ApiController]
    public class SystemController : Controller
    {
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IRuntimeInfo _runtimeInfo;
        private readonly IPlatformInfo _platformInfo;
        private readonly IOsInfo _osInfo;
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IConfigService _configService;
        private readonly IMainDatabase _database;
        private readonly IBackupService _backupService;
        private readonly ILifecycleService _lifecycleService;
        private readonly IDeploymentInfoProvider _deploymentInfoProvider;
        private readonly IMetadataSourceHealthService _metadataSourceHealthService;
        private readonly IUpdatePackageProvider _updatePackageProvider;
        private readonly EndpointDataSource _endpointData;
        private readonly DfaGraphWriter _graphWriter;
        private readonly DuplicateEndpointDetector _detector;

        public SystemController(IAppFolderInfo appFolderInfo,
                                IRuntimeInfo runtimeInfo,
                                IPlatformInfo platformInfo,
                                IOsInfo osInfo,
                                IConfigFileProvider configFileProvider,
                                IConfigService configService,
                                IMainDatabase database,
                                IBackupService backupService,
                                ILifecycleService lifecycleService,
                                IDeploymentInfoProvider deploymentInfoProvider,
                                IMetadataSourceHealthService metadataSourceHealthService,
                                IUpdatePackageProvider updatePackageProvider,
                                EndpointDataSource endpoints,
                                DfaGraphWriter graphWriter,
                                DuplicateEndpointDetector detector)
        {
            _appFolderInfo = appFolderInfo;
            _runtimeInfo = runtimeInfo;
            _platformInfo = platformInfo;
            _osInfo = osInfo;
            _configFileProvider = configFileProvider;
            _configService = configService;
            _database = database;
            _backupService = backupService;
            _lifecycleService = lifecycleService;
            _deploymentInfoProvider = deploymentInfoProvider;
            _metadataSourceHealthService = metadataSourceHealthService;
            _updatePackageProvider = updatePackageProvider;
            _endpointData = endpoints;
            _graphWriter = graphWriter;
            _detector = detector;
        }

        [HttpGet("status")]
        public SystemResource GetStatus()
        {
            return new SystemResource
            {
                AppName = BuildInfo.AppName,
                InstanceName = _configFileProvider.InstanceName,
                Version = BuildInfo.Version.ToString(),
                BuildTime = BuildInfo.BuildDateTime,
                IsDebug = BuildInfo.IsDebug,
                IsProduction = RuntimeInfo.IsProduction,
                IsAdmin = _runtimeInfo.IsAdmin,
                IsUserInteractive = RuntimeInfo.IsUserInteractive,
                StartupPath = _appFolderInfo.StartUpFolder,
                AppData = _appFolderInfo.GetAppDataPath(),
                OsName = _osInfo.Name,
                OsVersion = _osInfo.Version,
                IsNetCore = true,
                IsLinux = OsInfo.IsLinux,
                IsOsx = OsInfo.IsOsx,
                IsWindows = OsInfo.IsWindows,
                IsDocker = _osInfo.IsDocker,
                Mode = _runtimeInfo.Mode,
                Branch = _configFileProvider.Branch,
                Authentication = _configFileProvider.AuthenticationMethod,
                DatabaseType = _database.DatabaseType,
                DatabaseVersion = _database.Version,
                MigrationVersion = _database.Migration,
                UrlBase = _configFileProvider.UrlBase,
                RuntimeVersion = _platformInfo.Version,
                RuntimeName = "netcore",
                StartTime = _runtimeInfo.StartTime,
                PackageVersion = _deploymentInfoProvider.PackageVersion,
                PackageAuthor = _deploymentInfoProvider.PackageAuthor,
                PackageUpdateMechanism = _deploymentInfoProvider.PackageUpdateMechanism,
                PackageUpdateMechanismMessage = _deploymentInfoProvider.PackageUpdateMechanismMessage,
                DatabaseStatus = GetDatabaseStatusResource()
            };
        }

        [HttpGet("database")]
        public DatabaseStatusResource GetDatabaseStatus()
        {
            return GetDatabaseStatusResource();
        }

        [HttpGet("metadata")]
        public MetadataServiceStatusResource GetMetadataServiceStatus()
        {
            return GetMetadataServiceStatusResource();
        }

        [HttpGet("routes")]
        public IActionResult GetRoutes()
        {
            using (var sw = new StringWriter())
            {
                _graphWriter.Write(_endpointData, sw);
                var graph = sw.ToString();
                return Content(graph, "text/plain");
            }
        }

        [HttpGet("routes/duplicate")]
        public object DuplicateRoutes()
        {
            return _detector.GetDuplicateEndpoints(_endpointData);
        }

        private DatabaseStatusResource GetDatabaseStatusResource()
        {
            var sqlitePath = _appFolderInfo.GetDatabase();
            var sqliteFile = new FileInfo(sqlitePath);
            var postgresHostConfigured = _configFileProvider.PostgresHost.IsNotNullOrWhiteSpace();
            bool? postgresReachable = null;
            string postgresReachabilityMessage = null;

            if (postgresHostConfigured)
            {
                (postgresReachable, postgresReachabilityMessage) = CheckPostgresReachability();
            }

            return DatabaseStatusResourceMapper.ToResource(_database.DatabaseType,
                sqlitePath,
                sqliteFile.Exists,
                sqliteFile.Exists ? sqliteFile.Length : null,
                postgresHostConfigured,
                _configFileProvider.PostgresHost,
                _configFileProvider.PostgresPort,
                _configFileProvider.PostgresUser.IsNotNullOrWhiteSpace(),
                _configFileProvider.PostgresPassword.IsNotNullOrWhiteSpace(),
                _configFileProvider.PostgresMainDb,
                _configFileProvider.PostgresLogDb,
                _configFileProvider.PostgresCacheDb,
                postgresReachable,
                postgresReachabilityMessage,
                HasAnyBackup());
        }

        private (bool? Reachable, string Message) CheckPostgresReachability()
        {
            if (_configFileProvider.PostgresUser.IsNullOrWhiteSpace() ||
                _configFileProvider.PostgresPassword.IsNullOrWhiteSpace() ||
                _configFileProvider.PostgresMainDb.IsNullOrWhiteSpace())
            {
                return (null, "PostgreSQL configuration is incomplete.");
            }

            try
            {
                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = _configFileProvider.PostgresHost,
                    Port = _configFileProvider.PostgresPort,
                    Username = _configFileProvider.PostgresUser,
                    Password = _configFileProvider.PostgresPassword,
                    Database = _configFileProvider.PostgresMainDb,
                    Timeout = 3,
                    CommandTimeout = 3,
                    Enlist = false
                };

                using (var connection = new NpgsqlConnection(builder.ConnectionString))
                {
                    connection.Open();
                }

                return (true, "PostgreSQL target accepted a connection.");
            }
            catch (global::System.Exception ex)
            {
                return (false, $"PostgreSQL target is not reachable: {ex.GetType().Name}");
            }
        }

        private bool HasAnyBackup()
        {
            try
            {
                return _backupService.GetBackups().Any();
            }
            catch
            {
                return false;
            }
        }

        private MetadataServiceStatusResource GetMetadataServiceStatusResource()
        {
            var metadataSource = _configService.MetadataSource;
            var branch = _configFileProvider.Branch;
            MetadataSourceHealthResult healthResult;
            UpdatePackage latestUpdate = null;
            var updateCheckSucceeded = true;
            var updateCheckMessage = "ReadAIrr update metadata checked successfully.";

            try
            {
                healthResult = _metadataSourceHealthService.Test(metadataSource);
            }
            catch (global::System.Exception ex)
            {
                healthResult = new MetadataSourceHealthResult
                {
                    MetadataSource = metadataSource,
                    IsHealthy = false,
                    Message = "Metadata source health check failed",
                    Detail = ex.GetType().Name
                };
            }

            try
            {
                latestUpdate = _updatePackageProvider.GetLatestUpdate(branch, BuildInfo.Version);

                if (latestUpdate == null)
                {
                    updateCheckMessage = "No ReadAIrr app update is currently available.";
                }
            }
            catch (global::System.Exception ex)
            {
                updateCheckSucceeded = false;
                updateCheckMessage = $"ReadAIrr update metadata check failed: {ex.GetType().Name}";
            }

            return MetadataServiceStatusResourceMapper.ToResource(metadataSource,
                healthResult,
                latestUpdate,
                updateCheckMessage,
                updateCheckSucceeded,
                branch,
                BuildInfo.Version,
                global::System.DateTime.UtcNow);
        }

        [HttpPost("shutdown")]
        public object Shutdown()
        {
            Task.Factory.StartNew(() => _lifecycleService.Shutdown());
            return new { ShuttingDown = true };
        }

        [HttpPost("restart")]
        public object Restart()
        {
            Task.Factory.StartNew(() => _lifecycleService.Restart());
            return new { Restarting = true };
        }
    }
}
