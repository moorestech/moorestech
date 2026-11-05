namespace Client.Ui.Foundation.Layers
{
    // 層の固定順（先頭が最背面）。順の正本は web/app/tokens.css の --z-*。後続レーンは層を足すときここと UiLayerOrderTest の対応表を同時に直す
    // Fixed back-to-front order sourced from --z-* in web/app/tokens.css; later lanes add a layer here and in UiLayerOrderTest together
    public enum UiLayer
    {
        Backdrop,        // --z-viewport-backdrop
        BehindStage,     // --z-viewport-behind-stage（通知）
        Screen,          // --z-stage-screen
        Hud,             // --z-stage-overlay-panel / -chrome
        PauseMenu,       // --z-stage-pause-menu
        Tooltip,         // --z-stage-tooltip
        Grab,            // --z-viewport-grab
        WorldPin,        // --z-portal-world-pin
        Modal,           // --z-portal-modal
        Toast,           // --z-portal-toast
        Tutorial,        // --z-portal-tutorial
        Reconnect,       // --z-portal-reconnect
        FullScreenGate,  // --z-portal-full-screen-gate
    }
}
