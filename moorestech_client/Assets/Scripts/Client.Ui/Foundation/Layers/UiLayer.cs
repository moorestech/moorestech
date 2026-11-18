namespace Client.Ui.Foundation.Layers
{
    // 層の固定順（先頭が最背面）。正本は--z-*
    // Fixed back-to-front order; --z-* is the source
    public enum UiLayer
    {
        Backdrop,        // --z-viewport-backdrop
        BehindStage,     // --z-viewport-behind-stage（通知）
        ViewportStage,   // --z-viewport-stage
        Screen,          // --z-stage-screen
        OverlayPanel,    // --z-stage-overlay-panel
        Chrome,          // --z-stage-overlay-panel-chrome
        SkitStage,       // --z-stage-skit
        PauseMenu,       // --z-stage-pause-menu
        Tooltip,         // --z-stage-tooltip
        Grab,            // --z-viewport-grab
        WorldPin,        // --z-portal-world-pin（ここから Portal。z の無い .viewport の全層より前）
        Modal,           // --z-portal-modal
        SkitTransition,  // --z-portal-skit-transition
        Toast,           // --z-portal-toast
        Tutorial,        // --z-portal-tutorial
        Reconnect,       // --z-portal-reconnect
        FullScreenGate,  // --z-portal-full-screen-gate
    }
}
