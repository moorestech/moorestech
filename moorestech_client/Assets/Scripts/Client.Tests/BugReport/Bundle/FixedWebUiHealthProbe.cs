using Client.Game.InGame.BugReport.WebUiHealth;

namespace Client.Tests.BugReport.Bundle
{
    // 決めた値をそのまま返す偽の probe。Webホストを起動しないテストで書き出し口を組むのに使う
    // A fake probe returning a fixed reading, used to build the writer in tests that never start the web host
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
