namespace NzbDrone.Core.Analytics
{
    public interface IAnalyticsService
    {
        bool IsEnabled { get; }
        bool InstallIsActive { get; }
    }

    public class AnalyticsService : IAnalyticsService
    {
        public bool IsEnabled => false;
        public bool InstallIsActive => false;
    }
}
