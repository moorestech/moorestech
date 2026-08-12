using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Game.PlacementTarget;

namespace Client.Game.InGame.Hotbar
{
    /// <summary>
    ///     ホットバー割当Guidをマスタ設置対象＋現行BPから解決する
    ///     Resolves a hotbar assignment guid via the master placement catalog plus current blueprints
    /// </summary>
    public class HotbarPlacementTargetResolver
    {
        private readonly PlacementTargetCatalog _catalog;
        private readonly ClientBlueprintLibrary _blueprintLibrary;

        public HotbarPlacementTargetResolver(PlacementTargetCatalog catalog, ClientBlueprintLibrary blueprintLibrary)
        {
            _catalog = catalog;
            _blueprintLibrary = blueprintLibrary;
        }

        // BuildMenuEntryCatalog.CreateEntriesと同じ供給源（マスタカタログ＋現行BP一覧）から解決する
        // Resolves from the same supply sources as BuildMenuEntryCatalog.CreateEntries (master catalog plus current blueprints)
        public bool TryResolve(Guid id, out PlacementTargetEntry entry)
        {
            if (_catalog.TryGetMasterEntry(id, out entry)) return true;

            // マスタ未解決分は現行BP一覧から解決する（サーバー側HotbarAssignmentDatastore.IsResolvableと同じ判定源）
            // Falls back to the current blueprint list, matching server-side HotbarAssignmentDatastore.IsResolvable
            foreach (var (blueprintId, name) in _blueprintLibrary.BlueprintEntries)
            {
                if (blueprintId != id) continue;
                entry = new PlacementTargetEntry(id, PlacementTargetKind.Blueprint, name);
                return true;
            }

            entry = default;
            return false;
        }
    }
}
