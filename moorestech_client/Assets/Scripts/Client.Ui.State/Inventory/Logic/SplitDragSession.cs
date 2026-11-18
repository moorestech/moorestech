using System;
using System.Collections.Generic;
using Client.Ui.State.Inventory.Actions;

namespace Client.Ui.State.Inventory.Logic
{
    /// <summary>
    /// 分割ドラッグで通ったスロットを重複なしで集め、離したときに 1 回だけ渡す（Web splitDrag の写し）
    /// Collects unique slots crossed during a split drag and hands them over once on release (port of Web splitDrag)
    /// </summary>
    public sealed class SplitDragSession
    {
        private readonly List<PlayerSlotRef> _slots = new();

        public bool IsActive { get; private set; }

        public void Begin(PlayerSlotRef slot)
        {
            IsActive = true;
            _slots.Clear();
            _slots.Add(slot);
        }

        public void Enter(PlayerSlotRef slot)
        {
            // silent-ok: ドラッグ外のホバーと同じスロットへの再進入は行き先に足さない仕様 / 配り先の数は離した後の配分結果で見える
            if (!IsActive || _slots.Contains(slot)) return;
            _slots.Add(slot);
        }

        // 離したときの行き先。ドラッグしていなければ空（配分の数はコマンドが現在の持ち手と行き先数から決める）
        // Destinations on release; empty when not dragging (the command derives the share from the current grab and count)
        public IReadOnlyList<PlayerSlotRef> End()
        {
            if (!IsActive) return Array.Empty<PlayerSlotRef>();
            IsActive = false;
            var released = _slots.ToArray();
            _slots.Clear();
            return released;
        }
    }
}
