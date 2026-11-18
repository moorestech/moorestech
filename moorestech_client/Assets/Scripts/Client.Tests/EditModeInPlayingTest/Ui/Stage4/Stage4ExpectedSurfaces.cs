using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;

namespace Client.Tests.EditModeInPlayingTest.Ui.Stage4
{
    // 面の名前と、その面が載るべき列（列の外に出る面は ExpectsColumn が偽。空文字で表さず書き忘れと区別する）
    // A surface name and its stage column (ExpectsColumn is false outside every column; no empty string, so a missing name is distinguishable)
    public readonly struct Stage4Surface
    {
        public readonly string Name;
        public readonly bool ExpectsColumn;
        public readonly string ColumnName;

        private Stage4Surface(string name, bool expectsColumn, string columnName)
        {
            Name = name;
            ExpectsColumn = expectsColumn;
            ColumnName = columnName;
        }

        public static Stage4Surface InColumn(string name, string columnName)
        {
            return new Stage4Surface(name, true, columnName);
        }

        // 列の外の面。ColumnName は読まない（ExpectsColumn が偽のとき AssertScreenSet は列が無いことだけを見る）
        // A surface outside every column; ColumnName is never read (AssertScreenSet only checks that no column holds it)
        public static Stage4Surface OutsideColumns(string name)
        {
            return new Stage4Surface(name, false, null);
        }
    }

    // 画面ごとに見えるべき面の表（Web の App.tsx と UiScreenTraitsTable の写し）
    // Which surfaces each screen shows (copied from the web App.tsx and UiScreenTraitsTable)
    public static class Stage4ExpectedSurfaces
    {
        public static readonly Stage4Surface Inventory = Stage4Surface.InColumn("inventory-panel", "stage-column-inv");
        public static readonly Stage4Surface RecipeViewer = Stage4Surface.InColumn("recipe-viewer", "stage-column-viewer");
        public static readonly Stage4Surface ItemList = Stage4Surface.InColumn("item-list-panel", "stage-column-items");
        public static readonly Stage4Surface BlockInventory = Stage4Surface.InColumn("block-inventory", "stage-column-viewer");
        public static readonly Stage4Surface ResearchTree = Stage4Surface.OutsideColumns("research-tree");
        public static readonly Stage4Surface Equipment = Stage4Surface.OutsideColumns("equipment-slots");
        public static readonly Stage4Surface Backdrop = Stage4Surface.OutsideColumns("screen-backdrop");

        public static readonly IReadOnlyList<Stage4Surface> All = new[] { Inventory, RecipeViewer, ItemList, BlockInventory, ResearchTree, Equipment, Backdrop };

        public static IReadOnlyList<Stage4Surface> Expected(UIStateEnum state)
        {
            // 同時に出る組だけを持つ。他の画面は呼ばない
            // Holds only the co-shown sets; other screens are not asked
            return state switch
            {
                UIStateEnum.PlayerInventory => new[] { Inventory, RecipeViewer, ItemList, Equipment, Backdrop },
                UIStateEnum.SubInventory => new[] { Inventory, BlockInventory, Equipment, Backdrop },
                UIStateEnum.ResearchTree => new[] { Inventory, ResearchTree, Backdrop },
                UIStateEnum.GameScreen => new[] { Equipment },
                _ => throw new System.ArgumentOutOfRangeException(nameof(state), state, "S4 の同時に出る組の表に無い画面"),
            };
        }
    }
}
