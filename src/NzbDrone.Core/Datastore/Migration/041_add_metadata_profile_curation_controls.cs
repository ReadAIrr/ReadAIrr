using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(041)]
    public class add_metadata_profile_curation_controls : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("MetadataProfiles").AddColumn("RequireReadable").AsBoolean().WithDefaultValue(false);
            Alter.Table("MetadataProfiles").AddColumn("RequireAudio").AsBoolean().WithDefaultValue(false);
            Alter.Table("MetadataProfiles").AddColumn("SkipAnthologies").AsBoolean().WithDefaultValue(false);
            Alter.Table("MetadataProfiles").AddColumn("SkipCollections").AsBoolean().WithDefaultValue(false);
            Alter.Table("MetadataProfiles").AddColumn("SkipSerializedParts").AsBoolean().WithDefaultValue(false);
            Alter.Table("MetadataProfiles").AddColumn("SkipEssays").AsBoolean().WithDefaultValue(false);
            Alter.Table("MetadataProfiles").AddColumn("SkipShortStories").AsBoolean().WithDefaultValue(false);
        }
    }
}
