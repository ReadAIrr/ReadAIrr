using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(054)]
    public class add_unmapped_review_statuses : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (!Schema.Table("UnmappedFileReviewStatuses").Exists())
            {
                Create.Table("UnmappedFileReviewStatuses")
                    .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                    .WithColumn("BookFileId").AsInt32().NotNullable()
                    .WithColumn("Path").AsString().NotNullable()
                    .WithColumn("Size").AsInt64().NotNullable()
                    .WithColumn("Modified").AsDateTime().NotNullable()
                    .WithColumn("Status").AsString().NotNullable()
                    .WithColumn("ReasonKinds").AsString().Nullable()
                    .WithColumn("Confidence").AsInt32().Nullable()
                    .WithColumn("Source").AsString().NotNullable()
                    .WithColumn("Created").AsDateTime().NotNullable()
                    .WithColumn("Updated").AsDateTime().NotNullable();

                Create.Index().OnTable("UnmappedFileReviewStatuses")
                    .OnColumn("BookFileId")
                    .Ascending()
                    .WithOptions()
                    .Unique();

                Create.Index().OnTable("UnmappedFileReviewStatuses")
                    .OnColumn("Status")
                    .Ascending();
            }
        }
    }
}
