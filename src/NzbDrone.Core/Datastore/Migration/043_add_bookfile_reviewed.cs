using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(043)]
    public class add_bookfile_reviewed : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (!Schema.Table("BookFiles").Column("Reviewed").Exists())
            {
                Alter.Table("BookFiles").AddColumn("Reviewed").AsBoolean().WithDefaultValue(false);
            }
        }
    }
}
