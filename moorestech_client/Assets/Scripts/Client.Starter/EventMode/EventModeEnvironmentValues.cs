namespace Client.Starter.EventMode
{
    // 出展モード関連の環境変数の生値を名前付きで束ねる
    // Bundles the raw values of exhibition-mode env vars under explicit names
    public readonly struct EventModeEnvironmentValues
    {
        public readonly string Enable;
        public readonly string IdleTimeoutSeconds;
        public readonly string EditorOptIn;
        public readonly string Language;

        public EventModeEnvironmentValues(string enable, string idleTimeoutSeconds, string editorOptIn, string language)
        {
            Enable = enable;
            IdleTimeoutSeconds = idleTimeoutSeconds;
            EditorOptIn = editorOptIn;
            Language = language;
        }
    }
}
