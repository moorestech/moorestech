using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface.Extension;
using Game.UnlockState;

namespace Game.PlacementTarget
{
    // ビルドメニューに並ぶ全設置対象（ブロック・車両・接続ツール・ビルドツール・ブループリント）をGuidで解決するカタログ
    // Catalog resolving every build-menu placement target (block/train car/connect tool/build tool/blueprint) by Guid
    public class PlacementTargetCatalog
    {
        private readonly IBlueprintCatalogSource _blueprintSource;
        private readonly IReadOnlyList<PlacementTargetEntry> _masterEntries;

        public PlacementTargetCatalog(IBlueprintCatalogSource blueprintSource)
        {
            _blueprintSource = blueprintSource;
            _masterEntries = CreateMasterEntries();

            // Guid単体解決の誤配線を防ぐため、全種別で衝突を拒否する
            // Reject collisions across kinds to prevent incorrect GUID-only resolution
            ValidateMasterIdentity(_masterEntries);

            #region Internal

            List<PlacementTargetEntry> CreateMasterEntries()
            {
                var entries = new List<PlacementTargetEntry>();

                // 坂を除き表示優先度と名前で整列
                // Exclude slopes and sort by display priority and name
                var blocks = MasterHolder.BlockMaster.Blocks.Data
                    .Where(block => !BeltConveyorPlaceFamilyUtil.IsSlopeBlock(block.BlockGuid))
                    .OrderBy(block => block.SortPriority ?? 0)
                    .ThenBy(block => block.Name);
                foreach (var block in blocks)
                    entries.Add(new PlacementTargetEntry(block.BlockGuid, PlacementTargetKind.Block, block.Name));
                foreach (var trainCar in MasterHolder.TrainUnitMaster.Train.TrainCars)
                    entries.Add(new PlacementTargetEntry(trainCar.TrainCarGuid, PlacementTargetKind.TrainCar, trainCar.Name));
                foreach (var connectTool in MasterHolder.ConnectToolMaster.All.OrderBy(connectTool => connectTool.SortPriority))
                    entries.Add(new PlacementTargetEntry(connectTool.ConnectToolGuid, PlacementTargetKind.ConnectTool, connectTool.Name));
                foreach (var buildTool in MasterHolder.BuildToolMaster.All)
                    entries.Add(new PlacementTargetEntry(buildTool.BuildToolGuid, PlacementTargetKind.BuildTool, buildTool.Name));
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

        public IReadOnlyList<PlacementTargetEntry> CreateEntries()
        {
            // 検証済みマスタへ現行BPを追加
            // Append current blueprints to validated master entries
            var entries = new List<PlacementTargetEntry>(_masterEntries);
            var seenById = _masterEntries.ToDictionary(entry => entry.Id);
            foreach (var (id, name) in _blueprintSource.BlueprintEntries)
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
        public IReadOnlyList<PlacementTargetEntry> UnlockedEntries(IGameUnlockStateData unlockState, bool showAllPlaceable)
        {
            var entries = new List<PlacementTargetEntry>();
            foreach (var entry in CreateEntries())
            {
                if (IsUnlocked(entry)) entries.Add(entry);
            }

            return entries;

            #region Internal

            bool IsUnlocked(PlacementTargetEntry entry)
            {
                switch (entry.Kind)
                {
                    case PlacementTargetKind.Block:
                        return showAllPlaceable || (unlockState.BlockUnlockStateInfos.TryGetValue(entry.Id, out var blockInfo) && blockInfo.IsUnlocked);
                    case PlacementTargetKind.TrainCar:
                        return showAllPlaceable || (unlockState.TrainCarUnlockStateInfos.TryGetValue(entry.Id, out var trainCarInfo) && trainCarInfo.IsUnlocked);
                    case PlacementTargetKind.ConnectTool:
                        // 接続ツールは無料設置対象外
                        // Connect tools are excluded from free placement
                        return unlockState.ConnectToolUnlockStateInfos.TryGetValue(entry.Id, out var connectToolInfo) && connectToolInfo.IsUnlocked;
                    case PlacementTargetKind.BuildTool:
                    case PlacementTargetKind.Blueprint:
                        return true;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            #endregion
        }
    }
}
