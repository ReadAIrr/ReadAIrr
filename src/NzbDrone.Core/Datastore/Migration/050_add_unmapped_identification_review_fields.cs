using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(050)]
    public class add_unmapped_identification_review_fields : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("UnmappedFileIdentificationSuggestions")
                .AddColumn("Transcript").AsString().Nullable()
                .AddColumn("TranscriptIsTruncated").AsBoolean().WithDefaultValue(false)
                .AddColumn("StepLog").AsString().Nullable();
        }
    }
}
