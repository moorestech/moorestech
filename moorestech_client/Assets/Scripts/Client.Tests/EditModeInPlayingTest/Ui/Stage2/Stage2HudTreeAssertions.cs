using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;
using Client.Playtest.UiElements.GameUi;
using Client.Ui.Foundation.Root;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.EditModeInPlayingTest.Ui.Stage2
{
    /// <summary>
    /// 段階 2 HUD の表の行を UI の木で確かめる補助
    /// Helpers that check stage-2 HUD table rows against the UI tree
    /// </summary>
    internal static class Stage2HudTreeAssertions
    {
        // その UI 状態で載る行（常駐を含む）が期待の層に 1 つずつ載る
        // Each row mounted in the UI state (residents included) sits once under its layer
        public static void AssertMountedIn(VisualElement root, UIStateEnum state)
        {
            foreach (var surface in Stage2HudSurfaces.All)
            {
                if (!surface.IsMountedIn(state)) continue;
                var matches = Matches(root, surface.Name, surface.ByClass);
                Assert.AreEqual(1, matches.Count, $"{surface.Name} が {state} で {matches.Count} 件載っている（期待 1 件）");
                var layer = GameUiRoot.Instance.GetLayer(surface.Layer, surface.Family);
                Assert.IsTrue(layer.Contains(matches[0]), $"{surface.Name} が層 {surface.Layer} の {surface.Family} の下に無い");
            }
        }

        // 表の ByClass に従って引き、どれかが見えていれば真（判定は DSL の GameUiElementQuery）
        // True when any element found per the row's ByClass is visible (uses the DSL's GameUiElementQuery)
        public static bool Visible(VisualElement root, Stage2HudSurface surface)
        {
            foreach (var element in Matches(root, surface.Name, surface.ByClass))
            {
                if (GameUiElementQuery.IsVisible(element)) return true;
            }
            return false;
        }

        private static List<VisualElement> Matches(VisualElement root, string name, bool byClass)
        {
            return byClass ? root.Query<VisualElement>(className: name).ToList() : root.Query<VisualElement>(name: name).ToList();
        }
    }
}
