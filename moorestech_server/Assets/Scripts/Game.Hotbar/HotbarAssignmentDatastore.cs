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
            // マスタでも現行BPでも解決できないIDは受け付けない
            // Reject ids neither the master catalog nor current blueprints resolve
            if (!IsResolvable(targetId)) return;
            GetOrCreate(playerId)[slot] = targetId;
            _onAssignmentChanged.OnNext(playerId);
        }

        public void ClearAssignment(int playerId, int slot)
        {
            GetOrCreate(playerId)[slot] = Guid.Empty;
            _onAssignmentChanged.OnNext(playerId);
        }

        public void SwapAssignments(int playerId, int slotA, int slotB)
        {
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
                    var id = Guid.Parse(playerSave.Assignments[slot]);
                    // 解決できない割当はロード時に削除する（アンロック状態は見ない）
                    // Drop unresolvable assignments at load; unlock state is not consulted
                    slots[slot] = IsResolvable(id) ? id : Guid.Empty;
                }
            }
        }

        private bool IsResolvable(Guid id)
        {
            // 有効=マスタカタログ or 現行ブループリント
            // Valid ids come from the master catalog or current blueprints
            return _catalog.TryGetMasterEntry(id, out _) || _blueprintDatastore.Blueprints.Any(bp => bp.BlueprintGuid == id);
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
