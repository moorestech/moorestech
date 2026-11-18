using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 欄の行を縦に並べる器
    /// Vertical holder of a section's rows; rebuilds only when the row sequence changes, keeping elements on redraw
    /// </summary>
    public sealed class ContextRowList
    {
        private readonly List<VisualElement> _current = new();

        public UiStack Stack { get; }

        public ContextRowList(string testid, UiGap gap)
        {
            Stack = new UiStack(gap) { name = testid };
        }

        public void SetRows(IReadOnlyList<VisualElement> rows)
        {
            if (IsSameSequence(rows)) return;
            Stack.ClearItems();
            _current.Clear();
            foreach (var row in rows)
            {
                Stack.AddItem(row);
                _current.Add(row);
            }

            #region Internal

            bool IsSameSequence(IReadOnlyList<VisualElement> next)
            {
                if (next.Count != _current.Count) return false;
                for (var i = 0; i < next.Count; i++)
                {
                    if (next[i] != _current[i]) return false;
                }
                return true;
            }

            #endregion
        }

        // 欄の器へ 1 つだけ載せる（既に載っていれば何もしない）。行の無い欄は何も載せないので文の無いラベルが載らない
        // Mounts exactly one child on a section holder (no-op if already so); an empty section mounts nothing, so no text-less label attaches
        public static void MountOnly(VisualElement host, VisualElement child)
        {
            if (host.childCount == 1 && host[0] == child) return;
            host.Clear();
            host.Add(child);
        }
    }
}
