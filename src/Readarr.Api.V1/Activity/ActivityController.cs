using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.History;
using NzbDrone.Core.Instrumentation;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Queue;
using Readarr.Http;

namespace Readarr.Api.V1.Activity
{
    [V1ApiController]
    public class ActivityController : Controller
    {
        private const int MaxSourceRecords = 200;
        private const int MaxPageSize = 100;

        private readonly IHistoryService _historyService;
        private readonly ILogService _logService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IHealthCheckService _healthCheckService;
        private readonly IQueueService _queueService;
        private readonly IPendingReleaseService _pendingReleaseService;
        private readonly IUnmappedFileIdentificationSuggestionRepository _unmappedIdentificationSuggestionRepository;
        private readonly IContributorEvidenceRepository _contributorEvidenceRepository;

        public ActivityController(IHistoryService historyService,
                                  ILogService logService,
                                  IManageCommandQueue commandQueueManager,
                                  IHealthCheckService healthCheckService,
                                  IQueueService queueService,
                                  IPendingReleaseService pendingReleaseService,
                                  IUnmappedFileIdentificationSuggestionRepository unmappedIdentificationSuggestionRepository,
                                  IContributorEvidenceRepository contributorEvidenceRepository)
        {
            _historyService = historyService;
            _logService = logService;
            _commandQueueManager = commandQueueManager;
            _healthCheckService = healthCheckService;
            _queueService = queueService;
            _pendingReleaseService = pendingReleaseService;
            _unmappedIdentificationSuggestionRepository = unmappedIdentificationSuggestionRepository;
            _contributorEvidenceRepository = contributorEvidenceRepository;
        }

        [HttpGet]
        [Produces("application/json")]
        public ActivityPageResource GetActivity([FromQuery] PagingRequestResource paging,
                                                string term = null,
                                                string category = null,
                                                string level = null,
                                                string status = null,
                                                DateTime? start = null,
                                                DateTime? end = null)
        {
            var page = Math.Max(1, paging.Page ?? 1);
            var pageSize = Math.Min(MaxPageSize, Math.Max(1, paging.PageSize ?? 20));
            var sourceTake = Math.Min(MaxSourceRecords, Math.Max(pageSize, page * pageSize));
            var now = DateTime.UtcNow;
            var activity = new List<ActivityResource>();

            if (IncludeCategory(category, "history"))
            {
                activity.AddRange(GetHistory(sourceTake).Select(ActivityResourceMapper.FromHistory));
            }

            if (IncludeCategory(category, "command"))
            {
                activity.AddRange(_commandQueueManager.All()
                    .OrderByDescending(x => x.EndedAt ?? x.StartedAt ?? x.QueuedAt)
                    .Take(sourceTake)
                    .Select(ActivityResourceMapper.FromCommand));
            }

            if (IncludeCategory(category, "queue"))
            {
                activity.AddRange(_queueService.GetQueue()
                    .Concat(_pendingReleaseService.GetPendingQueue())
                    .Take(sourceTake)
                    .Select(x => ActivityResourceMapper.FromQueue(x, now)));
            }

            if (IncludeCategory(category, "health"))
            {
                activity.AddRange(_healthCheckService.Results()
                    .Where(x => x.Type != HealthCheckResult.Ok)
                    .Select(x => ActivityResourceMapper.FromHealth(x, now)));
            }

            if (IncludeCategory(category, "log"))
            {
                activity.AddRange(GetWarningLogs(sourceTake).Select(ActivityResourceMapper.FromLog));
            }

            if (IncludeCategory(category, "identification"))
            {
                activity.AddRange(_unmappedIdentificationSuggestionRepository.GetRecent(sourceTake)
                    .Select(ActivityResourceMapper.FromUnmappedIdentificationSuggestion));
            }

            if (IncludeCategory(category, "contributor"))
            {
                activity.AddRange(_contributorEvidenceRepository.GetRecent(sourceTake)
                    .Select(ActivityResourceMapper.FromContributorEvidence));
            }

            var filtered = activity
                .Where(x => ActivityResourceMapper.Matches(x, term, category, level, status, start, end));

            var sortDirection = paging.SortDirection ?? SortDirection.Descending;
            var sortKey = paging.SortKey ?? "time";
            var ordered = ApplySort(filtered, sortKey, sortDirection).ToList();

            return new ActivityPageResource
            {
                Page = page,
                PageSize = pageSize,
                SortKey = sortKey,
                SortDirection = sortDirection,
                TotalRecords = ordered.Count,
                Records = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                IncludedSources = new List<string>
                {
                    "History imports/download events",
                    "Command queue state",
                    "Current queue and pending releases",
                    "Current non-OK health checks",
                    "Recent warning/error/fatal log records",
                    "Recent unmapped AI/STT identification review states",
                    "Recent contributor/narrator evidence updates"
                },
                DeferredSources = new List<string>
                {
                    "Raw log file streaming",
                    "Full command request bodies",
                    "External telemetry",
                    "Unpersisted browser-only UI events",
                    "Raw transcripts, intro audio URLs, provider payloads, and full local file paths"
                },
                SafetyNote = "Activity is read-only, bounded, and redacts common secret-bearing query/header values. AI/STT rows show only review status and concise clues, not raw transcripts, provider payloads, audio URLs, or full paths."
            };
        }

        private List<EntityHistory> GetHistory(int take)
        {
            var spec = new PagingSpec<EntityHistory>
            {
                Page = 1,
                PageSize = take,
                SortKey = "date",
                SortDirection = SortDirection.Descending
            };

            return _historyService.Paged(spec).Records;
        }

        private List<Log> GetWarningLogs(int take)
        {
            var spec = new PagingSpec<Log>
            {
                Page = 1,
                PageSize = take,
                SortKey = "id",
                SortDirection = SortDirection.Descending
            };

            spec.FilterExpressions.Add(x => x.Level == "Fatal" || x.Level == "Error" || x.Level == "Warn");

            return _logService.Paged(spec).Records;
        }

        private static bool IncludeCategory(string selected, string category)
        {
            return string.IsNullOrWhiteSpace(selected) || string.Equals(selected, category, StringComparison.InvariantCultureIgnoreCase);
        }

        private static IOrderedEnumerable<ActivityResource> ApplySort(IEnumerable<ActivityResource> resources, string sortKey, SortDirection sortDirection)
        {
            var descending = sortDirection == SortDirection.Descending || sortDirection == SortDirection.Default;

            switch (sortKey)
            {
                case "category":
                    return descending ? resources.OrderByDescending(x => x.Category) : resources.OrderBy(x => x.Category);
                case "level":
                    return descending ? resources.OrderByDescending(x => x.Level) : resources.OrderBy(x => x.Level);
                case "status":
                    return descending ? resources.OrderByDescending(x => x.Status) : resources.OrderBy(x => x.Status);
                case "title":
                    return descending ? resources.OrderByDescending(x => x.Title) : resources.OrderBy(x => x.Title);
                default:
                    return descending ? resources.OrderByDescending(x => x.Time) : resources.OrderBy(x => x.Time);
            }
        }
    }
}
