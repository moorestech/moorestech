namespace Client.Game.InGame.UI.UIState
{
    /// <summary>
    /// CEF WebUI 表示中かどうかを一方通行で共有する静的ゲート。
    /// 書き込みは WebUiCefToggle のみ。UIStateControl / InGameCameraController が毎フレーム読み取り、遷移とカメラ操作を止める。
    /// One-way static gate telling whether the CEF WebUI is shown.
    /// Only WebUiCefToggle writes it; UIStateControl / InGameCameraController poll it each frame to stop transitions and camera control.
    /// </summary>
    public static class WebUiScreenGate
    {
        public static bool IsCefActive { get; private set; }

        public static void SetCefActive(bool active)
        {
            IsCefActive = active;
        }
    }
}
