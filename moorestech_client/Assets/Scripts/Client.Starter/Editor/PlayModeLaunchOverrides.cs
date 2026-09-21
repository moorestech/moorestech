#if UNITY_EDITOR
namespace Client.Starter.Editor
{
    /// <summary>
    /// 専用再生ボタンによる起動引数の上書きを1口へ束ねる。上書きは内蔵サーバー専用なのでリモート接続では何もしない。
    /// Bundles the dedicated play buttons' launch-arg overrides into one port; the overrides belong to the embedded server, so a remote connection gets none.
    /// </summary>
    public static class PlayModeLaunchOverrides
    {
        public static void ApplyIfNeeded(InitializeProprieties proprieties)
        {
            if (proprieties.IsRemoteConnection) return;

            // 専用再生ボタン時はセーブ無効化
            // Skip save/load for the dedicated play button
            SkipSaveLoadPlayModeSettings.ApplyIfNeeded(proprieties);

            // 生成ワールド起動引数を上書き
            // Override launch args for the generated-world play button
            GeneratedWorldPlayModeSettings.ApplyIfNeeded(proprieties);

            // 人が押した直Playは常時記録を有効にする
            // A direct play started by a person enables always-on capture
            DirectPlayAlwaysOnCaptureSettings.ApplyIfNeeded();
        }
    }
}
#endif
