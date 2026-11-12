using System.Collections.Generic;
using Client.Playtest.UiElements.GameUi;
using UnityEngine.UIElements;

namespace Client.Tests.EditModeInPlayingTest.Ui.Stage4
{
    // 根の木から面の見え方と置き場を読む（状態を持たない）
    // Reads how and where a surface shows from the root tree (stateless)
    public static class Stage4ScreenSetProbe
    {
        private const string ColumnNamePrefix = "stage-column-";

        public static int CountByName(VisualElement root, string name)
        {
            return root.Query(name: name).ToList().Count;
        }

        public static List<VisualElement> VisibleByName(VisualElement root, string name)
        {
            // 同名の全件から見えるものだけを残す（DSL のロケータと同じ全件の照会）
            // Keep only the visible ones among every same-named element (same full query as the DSL locator)
            var all = root.Query(name: name).ToList();
            all.RemoveAll(element => !IsVisible(element));
            return all;
        }

        // 見えるかの判定は DSL のロケータと同じ 1 か所を使う（InternalsVisibleTo("Client.Tests")）
        // Use the DSL locator's single visibility rule (InternalsVisibleTo("Client.Tests"))
        public static bool IsVisible(VisualElement element)
        {
            return GameUiElementQuery.IsVisible(element);
        }

        public static bool TryGetColumnName(VisualElement element, out string columnName)
        {
            // 祖先の列を探し、列の外なら偽を返す（空文字を「列の外」の合図に使わない）
            // Find the ancestor column; return false outside every column (no empty string as the outside signal)
            for (var current = element.parent; current != null; current = current.parent)
            {
                if (current.name == null || !current.name.StartsWith(ColumnNamePrefix)) continue;
                columnName = current.name;
                return true;
            }
            columnName = null;
            return false;
        }

        public static bool IsPickedAtCenter(VisualElement element)
        {
            // 中心で拾われた要素が自分か子孫なら覆われていない
            // Not covered when the element picked at the center is itself or a descendant
            var picked = element.panel.Pick(element.worldBound.center);
            for (var current = picked; current != null; current = current.parent)
            {
                if (current == element) return true;
            }
            return false;
        }

        public static string DescribePicked(VisualElement element)
        {
            var picked = element.panel.Pick(element.worldBound.center);
            return picked == null ? "（何も拾わない）" : $"{picked.name}（{string.Join(" ", picked.GetClasses())}）";
        }
    }
}
