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
            resource.Warnings.Should().BeEmpty();
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
        }
    }
}
