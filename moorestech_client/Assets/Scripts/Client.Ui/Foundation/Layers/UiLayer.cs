namespace Client.Ui.Foundation.Layers
{
    // 層の固定順（先頭が最背面）。順の正本は web/app/tokens.css・skitTokens.css の --z-*。後続レーンは層を足すときここと UiLayerOrderTest の対応表を同時に直す
    // Fixed back-to-front order sourced from --z-* in web/app/tokens.css and skitTokens.css; later lanes add a layer here and in UiLayerOrderTest together
    public enum UiLayer
    {
        Backdrop,        // --z-viewport-backdrop
        BehindStage,     // --z-viewport-behind-stage（通知）
        ViewportStage,   // --z-viewport-stage（舞台の z を持たない直置き）
        Screen,          // --z-stage-screen
        OverlayPanel,    // --z-stage-overlay-panel（研究パネル）
        Chrome,          // --z-stage-overlay-panel-chrome（.viewportOverlay: チャレンジ HUD・キーヒント・採掘バー）
        SkitStage,       // --z-stage-skit（.viewportOverlay の 31 に封じ込め）
        PauseMenu,       // --z-stage-pause-menu
        Tooltip,         // --z-stage-tooltip
        Grab,            // --z-viewport-grab
        WorldPin,        // --z-portal-world-pin
        Modal,           // --z-portal-modal
        SkitTransition,  // --z-portal-skit-transition
        Toast,           // --z-portal-toast
        Tutorial,        // --z-portal-tutorial
        Reconnect,       // --z-portal-reconnect
        FullScreenGate,  // --z-portal-full-screen-gate
    }
}
