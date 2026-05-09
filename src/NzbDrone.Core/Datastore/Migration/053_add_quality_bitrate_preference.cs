using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(053)]
    public class add_quality_bitrate_preference : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("QualityDefinitions")
                .AddColumn("BitratePreference").AsInt32().WithDefaultValue(0);
        }
    }
}
