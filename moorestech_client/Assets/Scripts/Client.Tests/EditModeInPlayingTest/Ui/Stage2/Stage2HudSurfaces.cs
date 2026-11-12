using Client.Game.InGame.UI.UIState;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;

namespace Client.Tests.EditModeInPlayingTest.Ui.Stage2
{
    /// <summary>
    /// 段階 2 HUD の根要素・層・UI 状態の 1 行
    /// One row: stage-2 HUD root, layer, and UI state
    /// </summary>
    internal readonly struct Stage2HudSurface
    {
        public readonly string Name;
        public readonly bool ByClass;
        public readonly UiLayer Layer;
        public readonly UiFamily Family;

        // 操作モード HUD は該当モードの間だけ木に載る（ModeHudView が出さない組を木から外す）。常駐は GameScreen
        // Operation-mode HUDs are in the tree only during their mode (ModeHudView unmounts hidden groups); residents use GameScreen
        public readonly UIStateEnum MountedState;

        public Stage2HudSurface(string name, bool byClass, UiLayer layer, UiFamily family, UIStateEnum mountedState)
        {
            Name = name;
            ByClass = byClass;
            Layer = layer;
            Family = family;
            MountedState = mountedState;
        }
    }

    /// <summary>
    /// 段階 2 の HUD の表（名前・層は各 Presenter の GetLayer と View の name で確かめた値）
    /// Table of stage-2 HUDs (names and layers checked against each presenter's GetLayer and view name)
    /// </summary>
    internal static class Stage2HudSurfaces
    {
        public static readonly Stage2HudSurface[] All =
        {
            new("crosshair", false, UiLayer.ViewportStage, UiFamily.Stage, UIStateEnum.GameScreen),
            new("key-hints", false, UiLayer.Chrome, UiFamily.Viewport, UIStateEnum.GameScreen),
            new("progress-bar", false, UiLayer.Chrome, UiFamily.Viewport, UIStateEnum.GameScreen),
            new("challenge-hud", false, UiLayer.Chrome, UiFamily.Viewport, UIStateEnum.GameScreen),
            new("train-riding-hud", true, UiLayer.Screen, UiFamily.Stage, UIStateEnum.GameScreen),
            new("screen-backdrop", false, UiLayer.Backdrop, UiFamily.Viewport, UIStateEnum.GameScreen),
            new("placement-mode-hud", false, UiLayer.Chrome, UiFamily.Viewport, UIStateEnum.PlaceBlock),
            new("delete-mode-warning", false, UiLayer.Chrome, UiFamily.Viewport, UIStateEnum.DeleteBar),
            new("delete-box-height-hud", false, UiLayer.Chrome, UiFamily.Viewport, UIStateEnum.DeleteBar),
            new("cursor-tooltip", false, UiLayer.Tooltip, UiFamily.Viewport, UIStateEnum.GameScreen),
            new("tutorial-overlay", false, UiLayer.Tutorial, UiFamily.Viewport, UIStateEnum.GameScreen),
            new("key-control-hint-hud", false, UiLayer.OverlayPanel, UiFamily.Viewport, UIStateEnum.GameScreen),
            new("world-pin-overlay", false, UiLayer.WorldPin, UiFamily.Viewport, UIStateEnum.GameScreen),
            new("notification-host", false, UiLayer.BehindStage, UiFamily.Viewport, UIStateEnum.GameScreen),
            new("blocking-skit", false, UiLayer.SkitStage, UiFamily.Viewport, UIStateEnum.GameScreen),
        };
    }
}
