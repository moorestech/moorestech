using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface.Extension;
using Game.UnlockState;

namespace Game.PlacementTarget
{
    // 設置対象のGuidカタログ
    // GUID catalog for placement targets
    public class PlacementTargetCatalog
    {
        private readonly IReadOnlyList<PlacementTargetEntry> _masterEntries;

        public PlacementTargetCatalog()
        {
            _masterEntries = CreateMasterEntries();

            // Guid単体解決の誤配線を防ぐため、全種別で衝突を拒否する
            // Reject collisions across kinds to prevent incorrect GUID-only resolution
            ValidateMasterIdentity(_masterEntries);

            #region Internal

            List<PlacementTargetEntry> CreateMasterEntries()
            {
                var entries = new List<PlacementTargetEntry>();

                // 表示優先度と名前で整列（坂ベルトも単体設置対象として載せる）
                // Sort by display priority and name; belt slopes are placeable targets too
                var blocks = MasterHolder.BlockMaster.Blocks.Data
                    .OrderBy(block => block.SortPriority ?? 0)
                    .ThenBy(block => block.Name);
                foreach (var block in blocks)
                    entries.Add(new PlacementTargetEntry(block.BlockGuid, PlacementTargetKind.Block, block.Name));
                foreach (var trainCar in MasterHolder.TrainUnitMaster.Train.TrainCars)
                    entries.Add(new PlacementTargetEntry(trainCar.TrainCarGuid, PlacementTargetKind.TrainCar, trainCar.Name));
                foreach (var connectTool in MasterHolder.ConnectToolMaster.All.OrderBy(connectTool => connectTool.SortPriority))
                    entries.Add(new PlacementTargetEntry(connectTool.ConnectToolGuid, PlacementTargetKind.ConnectTool, connectTool.Name));
                foreach (var buildTool in MasterHolder.BuildToolMaster.All)
                    entries.Add(new PlacementTargetEntry(buildTool.BuildToolGuid, PlacementTargetKind.BlueprintCopy, buildTool.Name));
                return entries;
            }

            void ValidateMasterIdentity(IReadOnlyList<PlacementTargetEntry> entries)
            {
                var seenById = new Dictionary<Guid, PlacementTargetEntry>();
                foreach (var entry in entries)
                {
                    if (entry.Id == Guid.Empty)
                        throw new InvalidOperationException($"PlacementTargetCatalog: Guid.Empty entry found (Kind={entry.Kind}, MasterDisplayName={entry.MasterDisplayName})");
                    if (seenById.TryGetValue(entry.Id, out var duplicated))
                        throw new InvalidOperationException($"PlacementTargetCatalog: duplicated Guid {entry.Id} between {duplicated.Kind} '{duplicated.MasterDisplayName}' and {entry.Kind} '{entry.MasterDisplayName}'");
                    seenById.Add(entry.Id, entry);
                }
            }

            #endregion
        }

        // マスタ由来エントリのみ解決（BPはIBlueprintDatastore側で判定する）
        // Resolves master-derived entries only (BPs are judged via IBlueprintDatastore)
        public bool TryGetMasterEntry(Guid id, out PlacementTargetEntry entry)
        {
            foreach (var masterEntry in _masterEntries)
            {
                if (masterEntry.Id != id) continue;
                entry = masterEntry;
                return true;
            }

            entry = default;
            return false;
        }

        public IReadOnlyList<PlacementTargetEntry> CreateEntries(IReadOnlyList<(Guid id, string name)> blueprintEntries)
        {
            // 検証済みマスタへ現行BPを追加
            // Append current blueprints to validated master entries
            var entries = new List<PlacementTargetEntry>(_masterEntries);
            var seenById = _masterEntries.ToDictionary(entry => entry.Id);
            foreach (var (id, name) in blueprintEntries)
            {
                var entry = new PlacementTargetEntry(id, PlacementTargetKind.Blueprint, name);
                ValidateBlueprintIdentity(entry, seenById);
                entries.Add(entry);
            }

            return entries;

            #region Internal

            void ValidateBlueprintIdentity(PlacementTargetEntry entry, Dictionary<Guid, PlacementTargetEntry> knownEntries)
            {
                if (entry.Id == Guid.Empty)
                    throw new InvalidOperationException($"PlacementTargetCatalog: Guid.Empty entry found (Kind={entry.Kind}, MasterDisplayName={entry.MasterDisplayName})");
                if (knownEntries.TryGetValue(entry.Id, out var duplicated))
                    throw new InvalidOperationException($"PlacementTargetCatalog: duplicated Guid {entry.Id} between {duplicated.Kind} '{duplicated.MasterDisplayName}' and {entry.Kind} '{entry.MasterDisplayName}'");
                knownEntries.Add(entry.Id, entry);
            }

            #endregion
        }

        // uGUIとWebの判定ずれによる未解放対象の露出を防ぐ
        // Centralize this to prevent locked-target exposure from UI drift
        public IReadOnlyList<PlacementTargetEntry> UnlockedEntries(IGameUnlockStateData unlockState, bool showAllPlaceable, IReadOnlyList<(Guid id, string name)> blueprintEntries)
        {
            var entries = new List<PlacementTargetEntry>();
            foreach (var entry in CreateEntries(blueprintEntries))
            {
                if (IsEntryUnlocked(entry, unlockState, showAllPlaceable)) entries.Add(entry);
            }

            return entries;
        }

        // このIDが実在するか。マスタか現行BPのどちらかに在ることを解放状態と無関係に判定する
        // Whether this id exists at all: present in the master catalog or among current blueprints, regardless of unlock state
        public bool IsResolvable(Guid id, IReadOnlyList<Guid> currentBlueprintIds)
        {
            return TryGetMasterEntry(id, out _) || currentBlueprintIds.Contains(id);
        }

        // このIDが今の解放状態で割当・使用可能か。実在確認も含め判定規則はここへ完全集約する（C1裁定）
        // Whether this id is assignable/usable under the current unlock state, existence included; the sole locus for this rule (C1 ruling)
        public bool IsAssignable(Guid id, IGameUnlockStateData unlockState, bool showAllPlaceable, IReadOnlyList<Guid> currentBlueprintIds)
        {
            if (TryGetMasterEntry(id, out var entry)) return IsEntryUnlocked(entry, unlockState, showAllPlaceable);
            // マスタ外は現行BPだけを通す。どこにも実在しないIDは割当不可
            // Outside the master, only current blueprints pass; an id that exists nowhere is never assignable
            return currentBlueprintIds.Contains(id) && unlockState.IsBlueprintUnlocked;
        }

        private static bool IsEntryUnlocked(PlacementTargetEntry entry, IGameUnlockStateData unlockState, bool showAllPlaceable)
        {
            switch (entry.Kind)
            {
                case PlacementTargetKind.Block:
                    // 坂ベルトは直線ブロックの解放状態に従う
                    // Belt slopes follow their family straight block's unlock state
                    var unlockGuid = BeltConveyorPlaceFamilyUtil.ResolveUnlockBlockGuid(entry.Id);
                    return showAllPlaceable || (unlockState.BlockUnlockStateInfos.TryGetValue(unlockGuid, out var blockInfo) && blockInfo.IsUnlocked);
                case PlacementTargetKind.TrainCar:
                    return showAllPlaceable || (unlockState.TrainCarUnlockStateInfos.TryGetValue(entry.Id, out var trainCarInfo) && trainCarInfo.IsUnlocked);
                case PlacementTargetKind.ConnectTool:
                    // 接続ツールは無料設置対象外
                    // Connect tools are excluded from free placement
                    return unlockState.ConnectToolUnlockStateInfos.TryGetValue(entry.Id, out var connectToolInfo) && connectToolInfo.IsUnlocked;
                case PlacementTargetKind.BlueprintCopy:
                case PlacementTargetKind.Blueprint:
                    // BP機能は単一フラグで判定。無料設置デバッグの対象外（接続ツール同様・ADR 0015）
                    // Blueprints gate on the single feature flag, excluded from free placement like connect tools (ADR 0015)
                    return unlockState.IsBlueprintUnlocked;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
