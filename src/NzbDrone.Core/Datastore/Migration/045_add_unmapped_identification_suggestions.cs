using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(045)]
    public class add_unmapped_identification_suggestions : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (!Schema.Table("UnmappedFileIdentificationSuggestions").Exists())
            {
                Create.Table("UnmappedFileIdentificationSuggestions")
                    .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                    .WithColumn("BookFileId").AsInt32().NotNullable()
                    .WithColumn("Path").AsString().NotNullable()
                    .WithColumn("Size").AsInt64().NotNullable()
                    .WithColumn("Modified").AsDateTime().NotNullable()
                    .WithColumn("Type").AsString().NotNullable()
                    .WithColumn("Provider").AsString().Nullable()
                    .WithColumn("Status").AsString().NotNullable()
                    .WithColumn("LikelyAuthor").AsString().Nullable()
                    .WithColumn("LikelyBook").AsString().Nullable()
                    .WithColumn("LikelyEdition").AsString().Nullable()
                    .WithColumn("Language").AsString().Nullable()
                    .WithColumn("Narrator").AsString().Nullable()
                    .WithColumn("Confidence").AsInt32().Nullable()
                    .WithColumn("Explanation").AsString().Nullable()
                    .WithColumn("RequiresManualConfirmation").AsBoolean().WithDefaultValue(true)
                    .WithColumn("TranscriptExcerpt").AsString().Nullable()
                    .WithColumn("ContextSummary").AsString().Nullable()
                    .WithColumn("Created").AsDateTime().NotNullable()
                    .WithColumn("Updated").AsDateTime().NotNullable();

                Create.Index().OnTable("UnmappedFileIdentificationSuggestions")
                    .OnColumn("BookFileId")
                    .Ascending();
            }
        }
    }
}
