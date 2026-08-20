using System;
using System.Collections.Generic;
using System.Linq;
using Common.Debug;
using Game.Blueprint;
using Game.PlacementTarget;
using Game.UnlockState;
using UniRx;

namespace Game.Hotbar
{
    // プレイヤー×9枠の設置対象参照ホットバー。カタログ/BP解決できないIDは保持しない
    // Player x 9-slot placement-target hotbar; ids the catalog/BPs can't resolve are never retained
    public class HotbarAssignmentDatastore : IHotbarAssignmentLookup, IHotbarAssignmentMutation
    {
        public const int SlotCount = 9;

        public IObservable<int> OnAssignmentChanged => _onAssignmentChanged;
        private readonly Subject<int> _onAssignmentChanged = new();

        // 未割当プレイヤーへ返す共有の空枠。読み取りだけでレコードを作らないための返り値
        // The shared empty-slot view returned for players with no record, so a read never creates one
        private static readonly Guid[] EmptySlots = new Guid[SlotCount];

        // プレイヤーごとの9枠。未割当はGuid.Empty
        // 9 slots per player; Guid.Empty means unassigned
        private readonly Dictionary<int, Guid[]> _assignments = new();
        private readonly PlacementTargetCatalog _catalog;
        private readonly IBlueprintDatastore _blueprintDatastore;
        private readonly IGameUnlockStateData _gameUnlockState;

        public HotbarAssignmentDatastore(PlacementTargetCatalog catalog, IBlueprintDatastore blueprintDatastore, IGameUnlockStateData gameUnlockState)
        {
            _catalog = catalog;
            _blueprintDatastore = blueprintDatastore;
            _gameUnlockState = gameUnlockState;

            // BP削除で解決不能になった枠をその場で捨てる。セッション中に死んだ参照が残らない
            // Drops slots that a blueprint deletion just made unresolvable, so no dead reference survives the session
            _blueprintDatastore.OnBlueprintDeleted.Subscribe(PruneDeletedBlueprint);
        }

        // 読み取りではレコードを作らない。参照しただけの空プレイヤーがセーブへ永続するのを防ぐ
        // A read never creates a record, so merely inspecting a player does not persist an empty entry into the save
        public IReadOnlyList<Guid> GetAssignments(int playerId)
        {
            return _assignments.TryGetValue(playerId, out var slots) ? slots : EmptySlots;
        }

        public void SetAssignment(int playerId, int slot, Guid targetId)
        {
            // 範囲外slotは不正クライアント対策として無視する
            // Out-of-range slots are ignored as a malicious-client defense
            if (!IsValidSlot(slot)) return;
            // 実在確認と未解放判定はカタログの1問い合わせへ畳む。ロード済み割当はLoadHotbar側で保持される（ADR 0015）
            // Existence and unlock checks collapse into one catalog query; loaded assignments are preserved by LoadHotbar (ADR 0015)
            if (!_catalog.IsAssignable(targetId, _gameUnlockState, IsFreeBlockPlacement(), CurrentBlueprintIds())) return;
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
            var currentBlueprintIds = CurrentBlueprintIds();
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
                return _catalog.IsResolvable(id, currentBlueprintIds) ? id : Guid.Empty;
            }

            #endregion
        }

        // 削除されたBPを指す枠を全プレイヤーから外し、変化したプレイヤーだけ通知する
        // Clears slots pointing at the deleted blueprint for every player, notifying only those actually changed
        private void PruneDeletedBlueprint(Guid deletedBlueprintGuid)
        {
            foreach (var pair in _assignments)
            {
                var slots = pair.Value;
                var pruned = false;
                for (var slot = 0; slot < SlotCount; slot++)
                {
                    if (slots[slot] != deletedBlueprintGuid) continue;
                    slots[slot] = Guid.Empty;
                    pruned = true;
                }

                if (pruned) _onAssignmentChanged.OnNext(pair.Key);
            }
        }

        // 判定のたびに現行BPのGuid一覧をカタログへ渡す（BPは実行中に増減するためキャッシュしない）
        // Hands the current blueprint GUIDs to the catalog per query; blueprints change at runtime so this is never cached
        private IReadOnlyList<Guid> CurrentBlueprintIds()
        {
            return _blueprintDatastore.Blueprints.Select(blueprint => blueprint.BlueprintGuid).ToList();
        }

        // 無料設置デバッグ中はビルドメニューの表示と割当可否を揃える
        // While the free-placement debug is on, keep assignability aligned with what the build menu shows
        private bool IsFreeBlockPlacement()
        {
            return DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);
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
