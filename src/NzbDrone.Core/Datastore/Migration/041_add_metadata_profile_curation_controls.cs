using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(041)]
    public class add_metadata_profile_curation_controls : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            AddBooleanColumnIfMissing("RequireReadable");
            AddBooleanColumnIfMissing("RequireAudio");
            AddBooleanColumnIfMissing("SkipAnthologies");
            AddBooleanColumnIfMissing("SkipCollections");
            AddBooleanColumnIfMissing("SkipSerializedParts");
            AddBooleanColumnIfMissing("SkipEssays");
            AddBooleanColumnIfMissing("SkipShortStories");
        }

        private void AddBooleanColumnIfMissing(string column)
        {
            if (!Schema.Table("MetadataProfiles").Column(column).Exists())
            {
                Alter.Table("MetadataProfiles").AddColumn(column).AsBoolean().WithDefaultValue(false);
            }
        }
    }
}
