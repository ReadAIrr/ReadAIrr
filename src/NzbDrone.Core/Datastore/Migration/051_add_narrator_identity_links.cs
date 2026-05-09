using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(051)]
    public class add_narrator_identity_links : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("NarratorIdentityLinks")
                  .WithColumn("CanonicalName").AsString().NotNullable()
                  .WithColumn("CanonicalNormalizedName").AsString().NotNullable()
                  .WithColumn("AliasName").AsString().NotNullable()
                  .WithColumn("AliasNormalizedName").AsString().NotNullable()
                  .WithColumn("RelationshipType").AsString().WithDefaultValue("alias")
                  .WithColumn("DisplayPreference").AsString().WithDefaultValue("canonical");

            Create.Index().OnTable("NarratorIdentityLinks").OnColumn("CanonicalNormalizedName");
            Create.Index().OnTable("NarratorIdentityLinks")
                  .OnColumn("AliasNormalizedName").Ascending()
                  .WithOptions().Unique();
        }
    }
}
