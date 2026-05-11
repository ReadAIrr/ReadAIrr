using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(055)]
    public class add_unmapped_narrator_validation_fields : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("UnmappedFileIdentificationSuggestions")
                .AddColumn("NarratorValidationStatus").AsString().Nullable()
                .AddColumn("NarratorValidationDetail").AsString().Nullable()
                .AddColumn("ValidatedNarrator").AsString().Nullable()
                .AddColumn("ValidatedForeignEditionId").AsString().Nullable()
                .AddColumn("ValidatedEditionTitle").AsString().Nullable();
        }
    }
}
