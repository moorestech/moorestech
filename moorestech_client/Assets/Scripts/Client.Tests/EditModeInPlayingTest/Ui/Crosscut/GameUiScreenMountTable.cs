using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.BlockInventory;
using Client.Ui.Screens.BuildMenu;
using Client.Ui.Screens.Challenge.CurrentHud;
using Client.Ui.Screens.Challenge.Tree;
using Client.Ui.Screens.CommonHud;
using Client.Ui.Screens.CursorTooltip;
using Client.Ui.Screens.GrabOverlay;
using Client.Ui.Screens.Hotbar;
using Client.Ui.Screens.Inventory.Equipment;
using Client.Ui.Screens.Inventory.Panel;
using Client.Ui.Screens.KeyHints;
using Client.Ui.Screens.ModeHud;
using Client.Ui.Screens.Notification;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.Screens.Progress;
using Client.Ui.Screens.Recipe;
using Client.Ui.Screens.Research;
using Client.Ui.Screens.Shell;
using Client.Ui.Screens.Skit;
using Client.Ui.Screens.Toast;
using Client.Ui.Screens.TrainHud;
using Client.Ui.Screens.Tutorial.KeyControlHint;
using Client.Ui.Screens.Tutorial.Overlay;
using Client.Ui.Screens.Tutorial.WorldPin;
using UnityEngine;

namespace Client.Tests.EditModeInPlayingTest.Ui.Crosscut
{
    // Presenter が載せる最上位の View と、その載り先の層（列に載る View は列を持つ Screen/Stage の層）
    // A top-level view a presenter mounts and its host layer (column views sit in the Screen/Stage layer that holds the columns)
    public readonly struct GameUiScreenMount
    {
        public readonly Type ViewType;
        public readonly UiLayer Layer;
        public readonly UiFamily Family;

        public GameUiScreenMount(Type viewType, UiLayer layer, UiFamily family)
        {
            ViewType = viewType;
            Layer = layer;
            Family = family;
        }
    }

    /// <summary>
    /// 全 Presenter の載せ先の表と、表の行数を Presenter の源の載せる行と突き合わせる数え方
    /// The table of every presenter's mount and the count that checks it against the mount lines in presenter sources
    /// </summary>
    public static class GameUiScreenMountTable
    {
        // 源の GetLayer(...) の行（定数で層を渡す HostLayer の形も含む）
        // GetLayer(...) lines in sources, including the HostLayer constant form
        private static readonly Regex LayerMountLine = new(@"GetLayer\((UiLayer\.\w+, UiFamily\.\w+|HostLayer, HostFamily)\)");

        // 源の舞台の列へ載せる行
        // Lines in sources that add to a stage column
        private static readonly Regex ColumnMountLine = new(@"\.Get\(StageColumn\.\w+\)\.Add\(");

        // GetLayer の行ごとに 1 行（スキットは本体と幕の 2 行）
        // One row per GetLayer line (the skit has two: the body and the transition)
        public static readonly IReadOnlyList<GameUiScreenMount> LayerMounts = new[]
        {
            new GameUiScreenMount(typeof(BuildMenuView), UiLayer.Screen, UiFamily.Stage),
            new GameUiScreenMount(typeof(CurrentChallengeHudView), UiLayer.Chrome, UiFamily.Viewport),
            new GameUiScreenMount(typeof(ChallengeListView), UiLayer.OverlayPanel, UiFamily.Stage),
            new GameUiScreenMount(typeof(CrosshairHudView), UiLayer.ViewportStage, UiFamily.Stage),
            new GameUiScreenMount(typeof(CursorTooltipView), UiLayer.Tooltip, UiFamily.Viewport),
            new GameUiScreenMount(typeof(GrabOverlayView), UiLayer.Grab, UiFamily.Viewport),
            new GameUiScreenMount(typeof(EquipmentHudView), UiLayer.Chrome, UiFamily.Viewport),
            new GameUiScreenMount(typeof(PauseMenuView), UiLayer.PauseMenu, UiFamily.Stage),
            new GameUiScreenMount(typeof(ProgressHudView), UiLayer.Chrome, UiFamily.Viewport),
            new GameUiScreenMount(typeof(ResearchTreeView), UiLayer.OverlayPanel, UiFamily.Stage),
            new GameUiScreenMount(typeof(ScreenBackdropView), UiLayer.Backdrop, UiFamily.Viewport),
            new GameUiScreenMount(typeof(SkitScreenView), UiLayer.SkitStage, UiFamily.Viewport),
            new GameUiScreenMount(typeof(SkitTransitionView), UiLayer.SkitTransition, UiFamily.Viewport),
            new GameUiScreenMount(typeof(ToastHostView), UiLayer.Toast, UiFamily.Viewport),
            new GameUiScreenMount(typeof(TrainRidingHudView), UiLayer.Screen, UiFamily.Stage),
            new GameUiScreenMount(typeof(KeyControlHintView), UiLayer.Chrome, UiFamily.Viewport),
            new GameUiScreenMount(typeof(TutorialOverlayView), UiLayer.Tutorial, UiFamily.Viewport),
            new GameUiScreenMount(typeof(WorldPinOverlayView), UiLayer.WorldPin, UiFamily.Viewport),
            new GameUiScreenMount(typeof(HotbarHudView), UiLayer.OverlayPanel, UiFamily.Viewport),
            new GameUiScreenMount(typeof(KeyHintHudView), UiLayer.Chrome, UiFamily.Viewport),
            new GameUiScreenMount(typeof(ModeHudView), UiLayer.OverlayPanel, UiFamily.Viewport),
            new GameUiScreenMount(typeof(NotificationHostView), UiLayer.BehindStage, UiFamily.Viewport),
        };

        // 列へ載せる行ごとに 1 行（列の入れ物は GameUiStageColumnsInstaller が Screen/Stage へ載せる）
        // One row per column mount line (GameUiStageColumnsInstaller puts the column container on Screen/Stage)
        public static readonly IReadOnlyList<GameUiScreenMount> ColumnMounts = new[]
        {
            new GameUiScreenMount(typeof(BlockInventoryView), UiLayer.Screen, UiFamily.Stage),
            new GameUiScreenMount(typeof(InventoryView), UiLayer.Screen, UiFamily.Stage),
            new GameUiScreenMount(typeof(RecipeViewerView), UiLayer.Screen, UiFamily.Stage),
            new GameUiScreenMount(typeof(ItemListView), UiLayer.Screen, UiFamily.Stage),
        };

        public static IEnumerable<GameUiScreenMount> All => LayerMounts.Concat(ColumnMounts);

        // 表と源の行数が違えば、足した・消した Presenter を表へ写し忘れている
        // A row-count mismatch means a presenter was added or removed without updating the table
        public static IEnumerable<string> DescribeTableDrift()
        {
            var sources = Directory.EnumerateFiles(Path.Combine(Application.dataPath, "Scripts", "Client.Ui", "Screens"), "*Presenter.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText).ToList();
            var layerLines = sources.Sum(text => LayerMountLine.Matches(text).Count);
            var columnLines = sources.Sum(text => ColumnMountLine.Matches(text).Count);
            if (layerLines != LayerMounts.Count) yield return $"GetLayer の行が源に {layerLines} 行、表に {LayerMounts.Count} 行";
            if (columnLines != ColumnMounts.Count) yield return $"列へ載せる行が源に {columnLines} 行、表に {ColumnMounts.Count} 行";
        }
    }
}
