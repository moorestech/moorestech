#if UNITY_EDITOR
namespace Client.Starter.Editor
{
    /// <summary>
    /// 記録設定と内蔵サーバーの起動上書きを束ねる。
    /// Bundles capture settings with embedded-server launch overrides.
    /// </summary>
    public static class PlayModeLaunchOverrides
    {
        public static void ApplyIfNeeded(InitializeProprieties proprieties)
        {
            // 接続先を問わず直Play記録を判断する
            // Decide direct-play capture regardless of destination
            DirectPlayAlwaysOnCaptureSettings.ApplyIfNeeded();
            if (proprieties.IsRemoteConnection) return;

            // 専用再生ボタン時はセーブ無効化
            // Skip save/load for the dedicated play button
            SkipSaveLoadPlayModeSettings.ApplyIfNeeded(proprieties);

            // 生成ワールド起動引数を上書き
            // Override launch args for the generated-world play button
            GeneratedWorldPlayModeSettings.ApplyIfNeeded(proprieties);

        }
    }
}
#endif
