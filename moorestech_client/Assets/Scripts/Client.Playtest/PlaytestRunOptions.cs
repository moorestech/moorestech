namespace Client.Playtest
{
    /// <summary>
    ///     シナリオ実行のオプション。フィールド初期値が既定値として機能する
    ///     Scenario run options; field initializers act as the defaults
    /// </summary>
    public class PlaytestRunOptions
    {
        public bool Record;
        public float ReadyTimeoutSeconds = 180f;
        public float ScenarioTimeoutSeconds = 300f;
    }
}
