using System;

namespace Game.Hotbar
{
    // 割当の変更口。プロトコル等の書き換え側だけがこちらへ依存する
    // The write side of the hotbar; only mutating callers such as protocols depend on this
    public interface IHotbarAssignmentMutation
    {
        void SetAssignment(int playerId, int slot, Guid targetId);
        void ClearAssignment(int playerId, int slot);
        void SwapAssignments(int playerId, int slotA, int slotB);
    }
}
