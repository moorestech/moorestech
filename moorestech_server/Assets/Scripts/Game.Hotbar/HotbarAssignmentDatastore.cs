using System;
using System.Collections.Generic;
using System.Linq;
using Game.Blueprint;
using Game.PlacementTarget;
using UniRx;

namespace Game.Hotbar
{
    // プレイヤー×9枠の設置対象参照ホットバー。カタログ/BP解決できないIDは保持しない
    // Player x 9-slot placement-target hotbar; ids the catalog/BPs can't resolve are never retained
    public class HotbarAssignmentDatastore
    {
        public const int SlotCount = 9;

        public IObservable<int> OnAssignmentChanged => _onAssignmentChanged;
        private readonly Subject<int> _onAssignmentChanged = new();

        // プレイヤーごとの9枠。未割当はGuid.Empty
        // 9 slots per player; Guid.Empty means unassigned
        private readonly Dictionary<int, Guid[]> _assignments = new();
        private readonly PlacementTargetCatalog _catalog;
        private readonly IBlueprintDatastore _blueprintDatastore;

        public HotbarAssignmentDatastore(PlacementTargetCatalog catalog, IBlueprintDatastore blueprintDatastore)
        {
            _catalog = catalog;
            _blueprintDatastore = blueprintDatastore;
        }

        public IReadOnlyList<Guid> GetAssignments(int playerId)
        {
            return GetOrCreate(playerId);
        }

        public void SetAssignment(int playerId, int slot, Guid targetId)
        {
            // 範囲外slot・未知IDはいずれも不正クライアント対策として無視する
            // Both out-of-range slots and unknown ids are ignored as malicious-client defenses
            if (!IsValidSlot(slot)) return;
            if (!IsResolvable(targetId)) return;
            GetOrCreate(playerId)[slot] = targetId;
            _onAssignmentChanged.OnNext(playerId);
        }

        public void ClearAssignment(int playerId, int slot)
        {
            if (!IsValidSlot(slot)) return;
            GetOrCreate(playerId)[slot] = Guid.Empty;
            _onAssignmentChanged.OnNext(playerId);
        }

        public void SwapAssignments(int playerId, int slotA, int slotB)
        {
            if (!IsValidSlot(slotA) || !IsValidSlot(slotB)) return;
            var slots = GetOrCreate(playerId);
            (slots[slotA], slots[slotB]) = (slots[slotB], slots[slotA]);
            _onAssignmentChanged.OnNext(playerId);
        }

        public List<PlayerHotbarSaveJsonObject> GetSaveJsonObject()
        {
            return _assignments
                .Select(pair => new PlayerHotbarSaveJsonObject(pair.Key, pair.Value.Select(id => id.ToString()).ToList()))
                .ToList();
        }

        public void LoadHotbar(List<PlayerHotbarSaveJsonObject> saveData)
        {
            _assignments.Clear();
            foreach (var playerSave in saveData)
            {
                var slots = GetOrCreate(playerSave.PlayerId);
                for (var slot = 0; slot < SlotCount; slot++)
                {
                    slots[slot] = ResolveSavedSlot(playerSave, slot);
                }
            }

            #region Internal

            Guid ResolveSavedSlot(PlayerHotbarSaveJsonObject playerSave, int slot)
            {
                // 件数不足・パース不能・未解決はすべて同じ扱いでGuid.Emptyへ落とす（形状不正で全体を落とさない）
                // Missing entries, unparsable strings, and unresolved ids all fall back to Guid.Empty so a malformed save never aborts the whole load
                if (playerSave.Assignments == null || playerSave.Assignments.Count != SlotCount) return Guid.Empty;
                if (!Guid.TryParse(playerSave.Assignments[slot], out var id)) return Guid.Empty;
                return IsResolvable(id) ? id : Guid.Empty;
            }

            #endregion
        }

        private bool IsResolvable(Guid id)
        {
            // 有効=マスタ or 現行BP
            // Valid ids come from the master catalog or current blueprints
            return _catalog.TryGetMasterEntry(id, out _) || _blueprintDatastore.Blueprints.Any(bp => bp.BlueprintGuid == id);
        }

        private bool IsValidSlot(int slot)
        {
            return 0 <= slot && slot < SlotCount;
        }

        private Guid[] GetOrCreate(int playerId)
        {
            if (_assignments.TryGetValue(playerId, out var slots)) return slots;
            slots = new Guid[SlotCount];
            _assignments[playerId] = slots;
            return slots;
        }
    }
}
