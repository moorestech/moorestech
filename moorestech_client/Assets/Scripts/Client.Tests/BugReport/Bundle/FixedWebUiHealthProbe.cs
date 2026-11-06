using Client.Game.InGame.BugReport.WebUiHealth;

namespace Client.Tests.BugReport.Bundle
{
    // 固定値を返す偽probe
    // Fake probe returning a fixed reading
    internal sealed class FixedWebUiHealthProbe : IWebUiHealthProbe
    {
        private readonly WebUiHealthReading _reading;

        public FixedWebUiHealthProbe(WebUiHealthReading reading)
        {
            _reading = reading;
        }

        public static FixedWebUiHealthProbe NotAttempted()
        {
            return new FixedWebUiHealthProbe(new WebUiHealthReading());
        }

        public WebUiHealthReading Read()
        {
            return _reading;
        }
    }
}
