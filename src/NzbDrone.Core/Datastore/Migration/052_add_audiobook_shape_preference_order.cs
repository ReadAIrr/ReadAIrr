using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(052)]
    public class add_audiobook_shape_preference_order : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("QualityProfiles")
                .AddColumn("AudiobookShapePreferenceOrder").AsString().WithDefaultValue("[]");
        }
    }
}
