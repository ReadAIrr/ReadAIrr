using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using Readarr.Api.V1.System;

namespace NzbDrone.Api.Test.System
{
    [TestFixture]
    public class DatabaseStatusResourceMapperFixture
    {
        [Test]
        public void should_report_sqlite_without_postgres_as_not_configured()
        {
            var resource = DatabaseStatusResourceMapper.ToResource(DatabaseType.SQLite,
                "/config/readarr.db",
                true,
                1024,
                false,
                null,
                5432,
                false,
                false,
                "readarr-main",
                "readarr-log",
                "readarr-cache",
                null,
                null,
                true);

            resource.ActiveProvider.Should().Be("SQLite");
            resource.ReadinessState.Should().Be("notConfigured");
            resource.SqlitePath.Should().Be("/config/readarr.db");
            resource.SqliteSizeLabel.Should().Be("1 KiB");
            resource.Postgres.IsConfigured.Should().BeFalse();
            resource.Postgres.Host.Should().BeNull();
            resource.MigrationGuidance.Should().Contain("manual SQLite to PostgreSQL migration");
            resource.RedactedTarget.Should().Be("PostgreSQL target not configured");
            resource.EnvironmentExample.Should().Contain("Readarr__Postgres__Password=<store in Azure Key Vault or deployment secret>");
            resource.EnvironmentExample.Should().NotContain("secret-value");
            resource.ConfigXmlExample.Should().Contain("&lt;secret value outside chat/docs&gt;");
            resource.Checklist.Should().Contain(x => x.Contains("Verify the latest backup"));
            resource.CopyableChecklist.Should().Contain("1. ");
            resource.Warnings.Should().ContainSingle(x => x.Contains("does not perform an automatic live SQLite to PostgreSQL conversion"));
        }

        [Test]
        public void should_report_reachable_postgres_target_without_exposing_credentials()
        {
            var resource = DatabaseStatusResourceMapper.ToResource(DatabaseType.SQLite,
                "/config/readarr.db",
                true,
                1024,
                true,
                "postgres.internal",
                5432,
                true,
                true,
                "readarr-main",
                "readarr-log",
                "readarr-cache",
                true,
                "PostgreSQL target accepted a connection.",
                true);

            resource.ReadinessState.Should().Be("reachable");
            resource.Postgres.IsConfigured.Should().BeTrue();
            resource.Postgres.Host.Should().Be("postgres.internal");
            resource.Postgres.UserConfigured.Should().BeTrue();
            resource.Postgres.PasswordConfigured.Should().BeTrue();
            resource.Postgres.MainDatabase.Should().Be("readarr-main");
            resource.Postgres.ReachabilityMessage.Should().NotContain("password");
            resource.RedactedTarget.Should().Be("postgres.internal:5432/readarr-main");
            resource.EnvironmentExample.Should().Contain("Readarr__Postgres__Host=postgres.internal");
            resource.EnvironmentExample.Should().Contain("Readarr__Postgres__Password=<store in Azure Key Vault or deployment secret>");
            resource.EnvironmentExample.Should().NotContain("password123");
            resource.Checklist.Should().Contain(x => x.Contains("Keep the reachable PostgreSQL target"));
        }

        [Test]
        public void should_warn_for_large_sqlite_without_backup()
        {
            var resource = DatabaseStatusResourceMapper.ToResource(DatabaseType.SQLite,
                "/config/readarr.db",
                true,
                600L * 1024L * 1024L,
                false,
                null,
                5432,
                false,
                false,
                "readarr-main",
                "readarr-log",
                "readarr-cache",
                null,
                null,
                false);

            resource.Warnings.Should().Contain(x => x.Contains("SQLite database is large"));
            resource.Warnings.Should().Contain(x => x.Contains("No backup was found"));
            resource.Warnings.Should().Contain(x => x.Contains("does not perform an automatic live SQLite to PostgreSQL conversion"));
        }

        [Test]
        public void should_block_incomplete_postgres_config()
        {
            var resource = DatabaseStatusResourceMapper.ToResource(DatabaseType.SQLite,
                "/config/readarr.db",
                true,
                1024,
                true,
                "postgres.internal",
                5432,
                false,
                false,
                "readarr-main",
                "readarr-log",
                "readarr-cache",
                null,
                "PostgreSQL configuration is incomplete.",
                true);

            resource.ReadinessState.Should().Be("blocked");
            resource.Warnings.Should().Contain(x => x.Contains("no user"));
            resource.Warnings.Should().Contain(x => x.Contains("no password"));
            resource.Checklist.Should().Contain(x => x.Contains("Verify PostgreSQL network access"));
        }

        [Test]
        public void should_report_active_postgres_as_already_migrated()
        {
            var resource = DatabaseStatusResourceMapper.ToResource(DatabaseType.PostgreSQL,
                "/config/readarr.db",
                true,
                1024,
                true,
                "postgres.internal",
                5432,
                true,
                true,
                "readarr-main",
                "readarr-log",
                "readarr-cache",
                true,
                "PostgreSQL target accepted a connection.",
                true);

            resource.ReadinessState.Should().Be("activePostgreSQL");
            resource.SqlitePath.Should().BeNull();
            resource.MigrationGuidance.Should().Contain("already running on PostgreSQL");
            resource.Checklist.Should().Contain(x => x.Contains("scheduled backups include PostgreSQL"));
            resource.Warnings.Should().NotContain(x => x.Contains("automatic live SQLite"));
        }
    }
}
