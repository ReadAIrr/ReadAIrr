using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(042)]
    public class add_author_identity_links : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("AuthorIdentityLinks")
                  .WithColumn("CanonicalAuthorId").AsInt32().NotNullable()
                  .WithColumn("AliasAuthorId").AsInt32().NotNullable()
                  .WithColumn("RelationshipType").AsString().WithDefaultValue("penName")
                  .WithColumn("DisplayPreference").AsString().WithDefaultValue("canonical");

            Create.Index().OnTable("AuthorIdentityLinks").OnColumn("CanonicalAuthorId");
            Create.Index().OnTable("AuthorIdentityLinks").OnColumn("AliasAuthorId");
            Create.Index().OnTable("AuthorIdentityLinks")
                  .OnColumn("CanonicalAuthorId").Ascending()
                  .OnColumn("AliasAuthorId").Ascending()
                  .WithOptions().Unique();
        }
    }
}
