using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.History;
using NzbDrone.Core.Instrumentation;
using NzbDrone.Core.MediaFiles;
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

        [Test]
        public void should_map_unmapped_identification_without_raw_transcript_or_full_path()
        {
            var resource = ActivityResourceMapper.FromUnmappedIdentificationSuggestion(new UnmappedFileIdentificationSuggestion
            {
                Id = 8,
                BookFileId = 99,
                Path = "/library/private/Author/Book/file.m4b",
                Type = "deepAudio",
                Provider = "OpenRouter",
                Status = "transcriptCaptured",
                Stage = "sttComplete",
                LikelyAuthor = "Jane Author",
                LikelyBook = "Example Book",
                Narrator = "Jane Reader",
                Confidence = 87,
                Explanation = "Matched intro clues",
                Transcript = "Raw transcript should never appear",
                TranscriptExcerpt = "Transcript excerpt should never appear",
                ProviderResponseExcerpt = "Provider body should never appear",
                Updated = new DateTime(2026, 5, 9, 4, 0, 0, DateTimeKind.Utc)
            });

            resource.Id.Should().Be("unmapped-identification-8");
            resource.Category.Should().Be("identification");
            resource.Level.Should().Be("info");
            resource.Status.Should().Be("transcriptCaptured");
            resource.Source.Should().Be("OpenRouter");
            resource.Title.Should().Be("Example Book - Jane Author");
            resource.Message.Should().Contain("87% confidence");
            resource.Message.Should().Contain("Matched intro clues");
            resource.RelatedId.Should().Be("99");
            resource.Message.Should().NotContain("/library/private");
            resource.Message.Should().NotContain("Raw transcript");
            resource.Message.Should().NotContain("Provider body");
        }

        [Test]
        public void should_map_running_unmapped_identification_as_current_state()
        {
            var resource = ActivityResourceMapper.FromUnmappedIdentificationSuggestion(new UnmappedFileIdentificationSuggestion
            {
                Id = 9,
                BookFileId = 100,
                Path = "/library/private/unknown.mp3",
                Status = "extracting",
                Updated = new DateTime(2026, 5, 9, 4, 5, 0, DateTimeKind.Utc)
            });

            resource.Category.Should().Be("identification");
            resource.Level.Should().Be("info");
            resource.Status.Should().Be("extracting");
            resource.Title.Should().Be("unknown.mp3");
            resource.IsCurrentState.Should().BeTrue();
        }

        [Test]
        public void should_map_contributor_evidence_without_raw_value()
        {
            var resource = ActivityResourceMapper.FromContributorEvidence(new ContributorEvidence
            {
                Id = 3,
                BookFileId = 44,
                Role = "narrator",
                DisplayName = "Jane Reader",
                Source = "sttTranscript",
                Confidence = 91,
                RawValue = "Raw narrator evidence should stay out of activity",
                Updated = new DateTime(2026, 5, 9, 4, 10, 0, DateTimeKind.Utc)
            });

            resource.Id.Should().Be("contributor-evidence-3");
            resource.Category.Should().Be("contributor");
            resource.Status.Should().Be("recorded");
            resource.Title.Should().Be("Jane Reader - narrator");
            resource.Message.Should().Contain("Source sttTranscript");
            resource.Message.Should().Contain("91% confidence");
            resource.Message.Should().NotContain("Raw narrator evidence");
            resource.RelatedId.Should().Be("44");
        }
    }
}
