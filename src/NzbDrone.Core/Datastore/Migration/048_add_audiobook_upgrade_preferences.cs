using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(048)]
    public class add_audiobook_upgrade_preferences : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("QualityProfiles")
                .AddColumn("AudiobookLayoutPreference").AsInt32().WithDefaultValue(0)
                .AddColumn("AudiobookFormatPreference").AsInt32().WithDefaultValue(0)
                .AddColumn("AudiobookFileCountPreference").AsInt32().WithDefaultValue(0);
        }
    }
}
