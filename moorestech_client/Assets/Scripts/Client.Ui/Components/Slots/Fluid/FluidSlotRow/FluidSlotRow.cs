using System.Collections.Generic;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Layout;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// 液体のマスの横並び＋任意の矢印
    /// A row of fluid cells with an optional progress arrow (the Web FluidSlotRow)
    /// </summary>
    public sealed class FluidSlotRow : VisualElement
    {
        // マス共通の固定 testid
        // The testid the Web FluidSlot always sets
        private const string SlotTestid = "fluid-slot";
        private const string EmptyStateClass = "fluid-slot-row--empty";

        private readonly UiGroup _group;
        private readonly List<FluidSlot> _slots = new();
        private readonly ProgressArrowBar _progress;
        private bool _progressShown;

        public FluidSlotRow(string testid)
        {
            name = testid;
            pickingMode = PickingMode.Ignore;
            AddToClassList("fluid-slot-row");
            AddToClassList(EmptyStateClass);
            _group = new UiGroup(UiGap.Xs);
            Add(_group);
            _progress = new ProgressArrowBar();
            _group.AddItem(_progress);
            _group.SetItemShown(_progress, false);
        }

        public void SetFluids(IReadOnlyList<FluidSlotContent> fluids)
        {
            // 位置ごとにマスを使い回す（Web の key=i）
            // Reuse cells by position, adding or removing the difference (the Web key=i)
            ResizeSlots(fluids.Count);
            for (var i = 0; i < fluids.Count; i++) _slots[i].SetFluid(fluids[i]);
            EnableInClassList(EmptyStateClass, fluids.Count == 0);

            #region Internal

            void ResizeSlots(int count)
            {
                // マスの数が同じなら矢印を入れ直さない（量の通知ごとの再取り付けを避ける）
                // Skip re-adding the arrow when the cell count is unchanged
                if (_slots.Count == count) return;
                while (count < _slots.Count)
                {
                    _slots[_slots.Count - 1].RemoveFromHierarchy();
                    _slots.RemoveAt(_slots.Count - 1);
                }
                while (_slots.Count < count)
                {
                    var slot = new FluidSlot(SlotTestid);
                    _slots.Add(slot);
                    _group.AddItem(slot);
                }
                // 足したマスの後ろへ矢印を戻す（AddItem の再追加は末尾へ移す）
                // Move the arrow back after added cells (re-adding moves it to the end)
                _group.AddItem(_progress);
                _group.SetItemShown(_progress, _progressShown);
            }

            #endregion
        }

        public void SetProgress(float ratio01)
        {
            _progress.SetValue(ratio01);
            _progressShown = true;
            _group.SetItemShown(_progress, true);
        }

        public void ClearProgress()
        {
            _progressShown = false;
            _group.SetItemShown(_progress, false);
        }
    }
}
