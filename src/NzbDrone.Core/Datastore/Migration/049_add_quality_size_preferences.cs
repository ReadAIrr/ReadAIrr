using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(049)]
    public class add_quality_size_preferences : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("QualityDefinitions")
                .AddColumn("EnforceSizeLimits").AsBoolean().WithDefaultValue(false)
                .AddColumn("TargetSize").AsDouble().Nullable()
                .AddColumn("SizePreference").AsInt32().WithDefaultValue(0);
        }
    }
}
