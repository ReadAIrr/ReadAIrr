using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.History;
using NzbDrone.Core.Instrumentation;
using NzbDrone.Core.Messaging.Commands;
using Readarr.Api.V1.Activity;

namespace NzbDrone.Api.Test.Activity
{
    [TestFixture]
    public class ActivityResourceMapperFixture
    {
        [Test]
        public void should_map_failed_history_to_warning_activity()
        {
            var resource = ActivityResourceMapper.FromHistory(new EntityHistory
            {
                Id = 12,
                Date = new DateTime(2026, 5, 9, 3, 0, 0, DateTimeKind.Utc),
                EventType = EntityHistoryEventType.DownloadFailed,
                SourceTitle = "Bad Release",
                DownloadId = "download-1"
            });

            resource.Id.Should().Be("history-12");
            resource.Category.Should().Be("history");
            resource.Level.Should().Be("warning");
            resource.Status.Should().Be("failed");
            resource.Title.Should().Be("Bad Release");
            resource.RelatedId.Should().Be("download-1");
            resource.IsCurrentState.Should().BeFalse();
        }

        [Test]
        public void should_not_expose_command_body_and_should_mark_running_command_current()
        {
            var resource = ActivityResourceMapper.FromCommand(new CommandModel
            {
                Id = 3,
                Name = "DeepIdentifyUnmappedFiles",
                Status = CommandStatus.Started,
                Result = CommandResult.Unknown,
                QueuedAt = new DateTime(2026, 5, 9, 2, 0, 0, DateTimeKind.Utc),
                StartedAt = new DateTime(2026, 5, 9, 2, 1, 0, DateTimeKind.Utc),
                Message = "Processing selected files",
                Trigger = CommandTrigger.Manual
            });

            resource.Id.Should().Be("command-3");
            resource.Category.Should().Be("command");
            resource.Status.Should().Be("started");
            resource.Level.Should().Be("info");
            resource.Title.Should().Be("Deep Identify Unmapped Files");
            resource.Message.Should().Be("Processing selected files");
            resource.IsCurrentState.Should().BeTrue();
        }

        [Test]
        public void should_redact_secret_values_from_log_messages()
        {
            var resource = ActivityResourceMapper.FromLog(new Log
            {
                Id = 9,
                Time = new DateTime(2026, 5, 9, 3, 15, 0, DateTimeKind.Utc),
                Level = "Error",
                Logger = "Readarr.Api",
                Message = "Request failed https://example.test?apikey=abc123 Authorization: BearerSecret"
            });

            resource.Category.Should().Be("log");
            resource.Level.Should().Be("error");
            resource.Message.Should().Contain("apikey=redacted");
            resource.Message.Should().Contain("Authorization: redacted");
            resource.Message.Should().NotContain("abc123");
            resource.Message.Should().NotContain("BearerSecret");
        }

        [Test]
        public void should_filter_by_category_level_status_and_term()
        {
            var resource = new ActivityResource
            {
                Time = new DateTime(2026, 5, 9, 3, 15, 0, DateTimeKind.Utc),
                Category = "health",
                Level = "warning",
                Status = "warning",
                Title = "Indexer Status",
                Message = "Indexer unavailable"
            };

            ActivityResourceMapper.Matches(resource, "indexer", "health", "warning", "warning", null, null).Should().BeTrue();
            ActivityResourceMapper.Matches(resource, "indexer", "history", "warning", "warning", null, null).Should().BeFalse();
            ActivityResourceMapper.Matches(resource, "missing", "health", "warning", "warning", null, null).Should().BeFalse();
        }

        [Test]
        public void should_map_health_check_as_current_state()
        {
            var now = new DateTime(2026, 5, 9, 3, 15, 0, DateTimeKind.Utc);
            var resource = ActivityResourceMapper.FromHealth(new HealthCheck(typeof(ActivityResourceMapperFixture), HealthCheckResult.Error, "Something broke"), now);

            resource.Category.Should().Be("health");
            resource.Level.Should().Be("error");
            resource.Status.Should().Be("error");
            resource.Time.Should().Be(now);
            resource.IsCurrentState.Should().BeTrue();
        }
    }
}
