using Client.Game.InGame.UI.UIState;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;

namespace Client.Tests.EditModeInPlayingTest.Ui.Stage2
{
    /// <summary>
    /// 段階 2 HUD の根要素・層・木に載る UI 状態の 1 行
    /// One row: stage-2 HUD root, layer, and the UI states it is mounted in
    /// </summary>
    internal readonly struct Stage2HudSurface
    {
        public readonly string Name;
        public readonly bool ByClass;
        public readonly UiLayer Layer;
        public readonly UiFamily Family;

        // 常駐は全状態で木に載る。操作モード HUD は該当モードの間だけ載る（ModeHudView が外す）
        // Residents stay mounted in every state; mode HUDs only during their mode (ModeHudView unmounts them)
        public readonly bool IsResident;
        private readonly UIStateEnum _modeState;

        private Stage2HudSurface(string name, bool byClass, UiLayer layer, UiFamily family, bool isResident, UIStateEnum modeState)
        {
            Name = name;
            ByClass = byClass;
            Layer = layer;
            Family = family;
            IsResident = isResident;
            _modeState = modeState;
        }

        public static Stage2HudSurface Resident(string name, bool byClass, UiLayer layer, UiFamily family)
        {
            // 常駐の行は _modeState を読まない
            // Resident rows never read _modeState
            return new Stage2HudSurface(name, byClass, layer, family, true, default);
        }

        public static Stage2HudSurface DuringMode(string name, UiLayer layer, UiFamily family, UIStateEnum modeState)
        {
            return new Stage2HudSurface(name, false, layer, family, false, modeState);
        }

        public bool IsMountedIn(UIStateEnum state)
        {
            return IsResident || _modeState == state;
        }
    }

    /// <summary>
    /// 段階 2 の HUD の表（名前・層は各 Presenter の GetLayer と View の name で確かめた値）
    /// Table of stage-2 HUDs (names and layers checked against each presenter's GetLayer and view name)
    /// </summary>
    internal static class Stage2HudSurfaces
    {
        public static readonly Stage2HudSurface Crosshair = Stage2HudSurface.Resident("crosshair", false, UiLayer.ViewportStage, UiFamily.Stage);
        public static readonly Stage2HudSurface KeyHints = Stage2HudSurface.Resident("key-hints", false, UiLayer.Chrome, UiFamily.Viewport);
        public static readonly Stage2HudSurface ProgressBar = Stage2HudSurface.Resident("progress-bar", false, UiLayer.Chrome, UiFamily.Viewport);
        public static readonly Stage2HudSurface ChallengeHud = Stage2HudSurface.Resident("challenge-hud", false, UiLayer.Chrome, UiFamily.Viewport);
        public static readonly Stage2HudSurface TrainRidingHud = Stage2HudSurface.Resident("train-riding-hud", true, UiLayer.Screen, UiFamily.Stage);
        public static readonly Stage2HudSurface ScreenBackdrop = Stage2HudSurface.Resident("screen-backdrop", false, UiLayer.Backdrop, UiFamily.Viewport);
        public static readonly Stage2HudSurface PlacementModeHud = Stage2HudSurface.DuringMode("placement-mode-hud", UiLayer.Chrome, UiFamily.Viewport, UIStateEnum.PlaceBlock);
        public static readonly Stage2HudSurface DeleteModeWarning = Stage2HudSurface.DuringMode("delete-mode-warning", UiLayer.Chrome, UiFamily.Viewport, UIStateEnum.DeleteBar);
        public static readonly Stage2HudSurface DeleteBoxHeightHud = Stage2HudSurface.DuringMode("delete-box-height-hud", UiLayer.Chrome, UiFamily.Viewport, UIStateEnum.DeleteBar);
        public static readonly Stage2HudSurface CursorTooltip = Stage2HudSurface.Resident("cursor-tooltip", false, UiLayer.Tooltip, UiFamily.Viewport);
        public static readonly Stage2HudSurface TutorialOverlay = Stage2HudSurface.Resident("tutorial-overlay", false, UiLayer.Tutorial, UiFamily.Viewport);
        public static readonly Stage2HudSurface KeyControlHintHud = Stage2HudSurface.Resident("key-control-hint-hud", false, UiLayer.OverlayPanel, UiFamily.Viewport);
        public static readonly Stage2HudSurface WorldPinOverlay = Stage2HudSurface.Resident("world-pin-overlay", false, UiLayer.WorldPin, UiFamily.Viewport);
        public static readonly Stage2HudSurface NotificationHost = Stage2HudSurface.Resident("notification-host", false, UiLayer.BehindStage, UiFamily.Viewport);
        public static readonly Stage2HudSurface BlockingSkit = Stage2HudSurface.Resident("blocking-skit", false, UiLayer.SkitStage, UiFamily.Viewport);

        public static readonly Stage2HudSurface[] All =
        {
            Crosshair, KeyHints, ProgressBar, ChallengeHud, TrainRidingHud, ScreenBackdrop,
            PlacementModeHud, DeleteModeWarning, DeleteBoxHeightHud,
            CursorTooltip, TutorialOverlay, KeyControlHintHud, WorldPinOverlay, NotificationHost, BlockingSkit,
        };
    }
}
