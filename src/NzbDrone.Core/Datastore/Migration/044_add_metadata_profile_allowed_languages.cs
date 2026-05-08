using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(044)]
    public class add_metadata_profile_allowed_languages : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (!Schema.Table("MetadataProfiles").Column("AllowedLanguages").Exists())
            {
                Alter.Table("MetadataProfiles").AddColumn("AllowedLanguages").AsString().Nullable();
            }
        }
    }
}
