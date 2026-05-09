using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.History;
using NzbDrone.Core.Instrumentation;
using NzbDrone.Core.Messaging.Commands;
using Readarr.Http;

namespace Readarr.Api.V1.Activity
{
    public class ActivityPageResource : PagingResource<ActivityResource>
    {
        public List<string> IncludedSources { get; set; }
        public List<string> DeferredSources { get; set; }
        public string SafetyNote { get; set; }
    }

    public class ActivityResource
    {
        public string Id { get; set; }
        public DateTime Time { get; set; }
        public string Category { get; set; }
        public string Type { get; set; }
        public string Level { get; set; }
        public string Status { get; set; }
        public string Source { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Entity { get; set; }
        public int? AuthorId { get; set; }
        public int? BookId { get; set; }
        public string RelatedId { get; set; }
        public bool IsCurrentState { get; set; }
    }

    public static class ActivityResourceMapper
    {
        private static readonly Regex QuerySecretRegex = new Regex(@"(?i)(api[_-]?key|apikey|token|access[_-]?token|password|secret)=([^&\s]+)", RegexOptions.Compiled);
        private static readonly Regex HeaderSecretRegex = new Regex(@"(?i)(Authorization|X-Api-Key|ApiKey|Password|Token):\s*([^\s,;]+)", RegexOptions.Compiled);

        public static ActivityResource FromHistory(EntityHistory model)
        {
            var authorName = model.Author?.Metadata?.Value?.Name;
            var bookTitle = model.Book?.Title;
            var entity = JoinParts(bookTitle, authorName);
            var message = model.EventType.ToString().SplitCamelCase();

            if (model.SourceTitle.IsNotNullOrWhiteSpace())
            {
                message = $"{message}: {model.SourceTitle}";
            }

            return new ActivityResource
            {
                Id = $"history-{model.Id}",
                Time = model.Date,
                Category = "history",
                Type = model.EventType.ToString(),
                Level = LevelFromHistory(model.EventType),
                Status = StatusFromHistory(model.EventType),
                Source = model.Data?.GetValueOrDefault(EntityHistory.INDEXER) ?? model.Data?.GetValueOrDefault(EntityHistory.DOWNLOAD_CLIENT) ?? "History",
                Title = model.SourceTitle.IsNotNullOrWhiteSpace() ? model.SourceTitle : entity,
                Message = Redact(Truncate(message)),
                Entity = entity,
                AuthorId = model.AuthorId > 0 ? model.AuthorId : null,
                BookId = model.BookId > 0 ? model.BookId : null,
                RelatedId = model.DownloadId,
                IsCurrentState = false
            };
        }

        public static ActivityResource FromCommand(CommandModel model)
        {
            var time = model.EndedAt ?? model.StartedAt ?? model.QueuedAt;
            var title = model.Name.SplitCamelCase();
            var message = model.Message.IsNotNullOrWhiteSpace() ? model.Message : model.Result.ToString();

            return new ActivityResource
            {
                Id = $"command-{model.Id}",
                Time = time,
                Category = "command",
                Type = model.Name,
                Level = LevelFromCommand(model.Status, model.Result),
                Status = model.Status.ToString().FirstCharToLower(),
                Source = model.Trigger.ToString(),
                Title = title,
                Message = Redact(Truncate(message)),
                RelatedId = model.Id.ToString(),
                IsCurrentState = model.EndedAt == null
            };
        }

        public static ActivityResource FromLog(Log model)
        {
            return new ActivityResource
            {
                Id = $"log-{model.Id}",
                Time = model.Time,
                Category = "log",
                Type = model.ExceptionType.IsNotNullOrWhiteSpace() ? model.ExceptionType : model.Logger,
                Level = NormalizeLevel(model.Level),
                Status = NormalizeLevel(model.Level),
                Source = model.Logger,
                Title = model.ExceptionType.IsNotNullOrWhiteSpace() ? model.ExceptionType : model.Logger,
                Message = Redact(Truncate(model.Message)),
                RelatedId = model.Id.ToString(),
                IsCurrentState = false
            };
        }

        public static ActivityResource FromHealth(HealthCheck model, DateTime now)
        {
            return new ActivityResource
            {
                Id = $"health-{model.Id}",
                Time = now,
                Category = "health",
                Type = model.Source.Name,
                Level = LevelFromHealth(model.Type),
                Status = model.Type.ToString().FirstCharToLower(),
                Source = "Health Check",
                Title = model.Source.Name.SplitCamelCase(),
                Message = Redact(Truncate(model.Message)),
                RelatedId = model.Id.ToString(),
                IsCurrentState = true
            };
        }

        public static ActivityResource FromQueue(NzbDrone.Core.Queue.Queue model, DateTime now)
        {
            var title = model.Title.IsNotNullOrWhiteSpace() ? model.Title : JoinParts(model.Book?.Title, model.Author?.Metadata?.Value?.Name);
            var statusMessage = model.ErrorMessage.IsNotNullOrWhiteSpace() ? model.ErrorMessage : model.Status;

            return new ActivityResource
            {
                Id = $"queue-{model.Id}",
                Time = model.EstimatedCompletionTime ?? now,
                Category = "queue",
                Type = model.Protocol.ToString(),
                Level = model.ErrorMessage.IsNotNullOrWhiteSpace() ? "warning" : "info",
                Status = model.Status?.FirstCharToLower(),
                Source = model.DownloadClient.IsNotNullOrWhiteSpace() ? model.DownloadClient : "Queue",
                Title = title,
                Message = Redact(Truncate(statusMessage)),
                Entity = JoinParts(model.Book?.Title, model.Author?.Metadata?.Value?.Name),
                AuthorId = model.Author?.Id,
                BookId = model.Book?.Id,
                RelatedId = model.DownloadId,
                IsCurrentState = true
            };
        }

        public static bool Matches(ActivityResource resource, string term, string category, string level, string status, DateTime? start, DateTime? end)
        {
            if (category.IsNotNullOrWhiteSpace() && !string.Equals(resource.Category, category, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            if (level.IsNotNullOrWhiteSpace() && !string.Equals(resource.Level, level, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            if (status.IsNotNullOrWhiteSpace() && !string.Equals(resource.Status, status, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            if (start.HasValue && resource.Time < start.Value)
            {
                return false;
            }

            if (end.HasValue && resource.Time > end.Value)
            {
                return false;
            }

            if (term.IsNullOrWhiteSpace())
            {
                return true;
            }

            var search = term.Trim();

            return Contains(resource.Title, search) ||
                   Contains(resource.Message, search) ||
                   Contains(resource.Source, search) ||
                   Contains(resource.Entity, search) ||
                   Contains(resource.Type, search) ||
                   Contains(resource.RelatedId, search);
        }

        private static string LevelFromHistory(EntityHistoryEventType eventType)
        {
            switch (eventType)
            {
                case EntityHistoryEventType.DownloadFailed:
                case EntityHistoryEventType.BookImportIncomplete:
                case EntityHistoryEventType.DownloadIgnored:
                    return "warning";
                case EntityHistoryEventType.BookFileDeleted:
                    return "info";
                default:
                    return "info";
            }
        }

        private static string StatusFromHistory(EntityHistoryEventType eventType)
        {
            switch (eventType)
            {
                case EntityHistoryEventType.DownloadFailed:
                    return "failed";
                case EntityHistoryEventType.BookImportIncomplete:
                    return "incomplete";
                case EntityHistoryEventType.DownloadIgnored:
                    return "ignored";
                case EntityHistoryEventType.Grabbed:
                    return "grabbed";
                case EntityHistoryEventType.BookFileImported:
                case EntityHistoryEventType.DownloadImported:
                    return "imported";
                default:
                    return eventType.ToString().FirstCharToLower();
            }
        }

        private static string LevelFromCommand(CommandStatus status, CommandResult result)
        {
            if (status == CommandStatus.Failed || result == CommandResult.Unsuccessful)
            {
                return "error";
            }

            if (status == CommandStatus.Aborted || status == CommandStatus.Cancelled || status == CommandStatus.Orphaned)
            {
                return "warning";
            }

            return "info";
        }

        private static string LevelFromHealth(HealthCheckResult result)
        {
            switch (result)
            {
                case HealthCheckResult.Error:
                    return "error";
                case HealthCheckResult.Warning:
                    return "warning";
                case HealthCheckResult.Notice:
                    return "notice";
                default:
                    return "info";
            }
        }

        private static string NormalizeLevel(string level)
        {
            if (level.IsNullOrWhiteSpace())
            {
                return "info";
            }

            return level.ToLowerInvariant() == "warn" ? "warning" : level.ToLowerInvariant();
        }

        private static bool Contains(string value, string term)
        {
            return value.IsNotNullOrWhiteSpace() && value.IndexOf(term, StringComparison.InvariantCultureIgnoreCase) >= 0;
        }

        private static string JoinParts(params string[] parts)
        {
            return string.Join(" - ", parts.Where(x => x.IsNotNullOrWhiteSpace()));
        }

        private static string Truncate(string value)
        {
            if (value.IsNullOrWhiteSpace() || value.Length <= 300)
            {
                return value;
            }

            return value.Substring(0, 300) + "...";
        }

        private static string Redact(string value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return value;
            }

            var redacted = QuerySecretRegex.Replace(value, "$1=redacted");
            return HeaderSecretRegex.Replace(redacted, "$1: redacted");
        }
    }
}
