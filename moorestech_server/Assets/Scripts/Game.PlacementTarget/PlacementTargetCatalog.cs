using System;
using System.Collections.Generic;
using System.Linq;
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

                // ベルトの坂はメニューに出さないため除外し、sortPriority→名前の表示順で並べる
                // Belt slopes never appear in the menu, so exclude them; order by sortPriority then name for display
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
                foreach (var (id, name) in _blueprintSource.BlueprintEntries)
                    entries.Add(new PlacementTargetEntry(id, PlacementTargetKind.Blueprint, name));

                // 生Guidだけが識別子のため、全供給元横断の非Empty・一意をここで保証する
                // The raw Guid is the sole identifier, so non-emptiness and uniqueness across every source are guaranteed here
                ValidateIdentity(entries);
                return entries;
            }
        }

        // 破ると TryGetEntry とビルドメニューのId照合が別対象へ無言で解決するため、kindでの救済はせず即例外にする
        // A violation would silently resolve TryGetEntry and the build-menu id lookup to a different target, so this throws instead of disambiguating by kind
        private static void ValidateIdentity(IReadOnlyList<PlacementTargetEntry> entries)
        {
            var seenById = new Dictionary<Guid, PlacementTargetEntry>();
            foreach (var entry in entries)
            {
                if (entry.Id == Guid.Empty)
                    throw new InvalidOperationException($"PlacementTargetCatalog: Guid.Empty entry found (Kind={entry.Kind}, DisplayName={entry.DisplayName})");
                if (seenById.TryGetValue(entry.Id, out var duplicated))
                    throw new InvalidOperationException($"PlacementTargetCatalog: duplicated Guid {entry.Id} between {duplicated.Kind} '{duplicated.DisplayName}' and {entry.Kind} '{entry.DisplayName}'");
                seenById.Add(entry.Id, entry);
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
