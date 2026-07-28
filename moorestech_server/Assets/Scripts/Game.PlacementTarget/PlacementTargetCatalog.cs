using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface.Extension;

namespace Game.PlacementTarget
{
    // ビルドメニューに並ぶ全設置対象（ブロック・車両・接続ツール・ビルドツール・ブループリント）をGuidで解決するカタログ
    // Catalog resolving every build-menu placement target (block/train car/connect tool/build tool/blueprint) by Guid
    public class PlacementTargetCatalog
    {
        private readonly IBlueprintCatalogSource _blueprintSource;

        public PlacementTargetCatalog(IBlueprintCatalogSource blueprintSource)
        {
            _blueprintSource = blueprintSource;
        }

        public IReadOnlyList<PlacementTargetEntry> Entries
        {
            get
            {
                // マスタ由来のエントリを列挙し、末尾に現在のブループリントを足す
                // Enumerate master-derived entries, then append current blueprints
                var entries = new List<PlacementTargetEntry>();
                foreach (var block in MasterHolder.BlockMaster.Blocks.Data)
                {
                    // WebBuildMenuEntryCatalog/BuildMenuEntryCatalogと同様、ベルトの坂はメニューに出さないため除外
                    // Excluded like WebBuildMenuEntryCatalog/BuildMenuEntryCatalog: belt slopes never appear in the build menu
                    if (BeltConveyorPlaceFamilyUtil.IsSlopeBlock(block.BlockGuid)) continue;
                    entries.Add(new PlacementTargetEntry(block.BlockGuid, PlacementTargetKind.Block, block.Name));
                }
                foreach (var trainCar in MasterHolder.TrainUnitMaster.Train.TrainCars)
                    entries.Add(new PlacementTargetEntry(trainCar.TrainCarGuid, PlacementTargetKind.TrainCar, trainCar.Name));
                foreach (var connectTool in MasterHolder.ConnectToolMaster.All)
                    entries.Add(new PlacementTargetEntry(connectTool.ConnectToolGuid, PlacementTargetKind.ConnectTool, connectTool.Name));
                foreach (var buildTool in MasterHolder.BuildToolMaster.All)
                    entries.Add(new PlacementTargetEntry(buildTool.BuildToolGuid, PlacementTargetKind.BuildTool, buildTool.Name));
                foreach (var (id, name) in _blueprintSource.BlueprintEntries)
                    entries.Add(new PlacementTargetEntry(id, PlacementTargetKind.Blueprint, name));
                return entries;
            }
        }

        public bool TryGetEntry(Guid id, out PlacementTargetEntry entry)
        {
            foreach (var e in Entries)
            {
                if (e.Id != id) continue;
                entry = e;
                return true;
            }
            entry = default;
            return false;
        }
    }
}
