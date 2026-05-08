using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(046)]
    public class add_unmapped_identification_debug_fields : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("UnmappedFileIdentificationSuggestions")
                .AddColumn("Stage").AsString().Nullable()
                .AddColumn("ProviderEndpoint").AsString().Nullable()
                .AddColumn("ProviderModel").AsString().Nullable()
                .AddColumn("ProviderStatusCode").AsInt32().Nullable()
                .AddColumn("ProviderDurationMs").AsInt32().Nullable()
                .AddColumn("ProviderResponseExcerpt").AsString().Nullable();
        }
    }
}
