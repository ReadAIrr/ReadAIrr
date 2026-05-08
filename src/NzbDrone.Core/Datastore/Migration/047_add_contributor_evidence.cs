using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(047)]
    public class add_contributor_evidence : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (!Schema.Table("ContributorEvidence").Exists())
            {
                Create.Table("ContributorEvidence")
                    .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                    .WithColumn("BookFileId").AsInt32().Nullable()
                    .WithColumn("EditionId").AsInt32().Nullable()
                    .WithColumn("ForeignEditionId").AsString().Nullable()
                    .WithColumn("Role").AsString().NotNullable()
                    .WithColumn("DisplayName").AsString().NotNullable()
                    .WithColumn("NormalizedName").AsString().NotNullable()
                    .WithColumn("Source").AsString().NotNullable()
                    .WithColumn("Confidence").AsInt32().Nullable()
                    .WithColumn("RawValue").AsString().Nullable()
                    .WithColumn("Created").AsDateTime().NotNullable()
                    .WithColumn("Updated").AsDateTime().NotNullable();

                Create.Index().OnTable("ContributorEvidence")
                    .OnColumn("BookFileId")
                    .Ascending();

                Create.Index().OnTable("ContributorEvidence")
                    .OnColumn("EditionId")
                    .Ascending();

                Create.Index().OnTable("ContributorEvidence")
                    .OnColumn("Role")
                    .Ascending()
                    .OnColumn("NormalizedName")
                    .Ascending();
            }
        }
    }
}
