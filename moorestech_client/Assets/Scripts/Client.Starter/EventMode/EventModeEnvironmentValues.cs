namespace Client.Starter.EventMode
{
    // 出展モード関連の環境変数の生値を名前付きで束ねる
    // Bundles the raw values of exhibition-mode env vars under explicit names
    // ctorを持たずオブジェクト初期化子だけで組ませ、同型stringの順序事故を構造的に消す
    // Having no ctor forces object initializers, structurally removing same-typed string ordering accidents
    public struct EventModeEnvironmentValues
    {
        public string Enable;
        public string IdleTimeoutSeconds;
        public string EditorOptIn;
        public string Language;
    }
}
