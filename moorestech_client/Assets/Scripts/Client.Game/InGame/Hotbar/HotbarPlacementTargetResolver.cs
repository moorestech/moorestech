using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Common.Debug;
using Game.PlacementTarget;
using Game.UnlockState;

namespace Client.Game.InGame.Hotbar
{
    /// <summary>
    ///     ホットバー割当Guidをビルドメニューと同一供給源から解決する
    ///     Resolves a hotbar assignment guid from the same supply source as the build menu
    /// </summary>
    public class HotbarPlacementTargetResolver
    {
        private readonly PlacementTargetCatalog _catalog;
        private readonly ClientBlueprintLibrary _blueprintLibrary;
        private readonly IGameUnlockStateData _gameUnlockStateData;

        public HotbarPlacementTargetResolver(PlacementTargetCatalog catalog, ClientBlueprintLibrary blueprintLibrary, IGameUnlockStateData gameUnlockStateData)
        {
            _catalog = catalog;
            _blueprintLibrary = blueprintLibrary;
            _gameUnlockStateData = gameUnlockStateData;
        }

        // ビルドメニュー(BuildMenuEntryCatalog.CreateEntries)と同じPlacementTargetCatalog.UnlockedEntriesを経由する
        // 未解放対象は解決しない（割当自体はセーブに残るが、使用時は建築モードに入れない）
        // Routes through the same PlacementTargetCatalog.UnlockedEntries the build menu uses (BuildMenuEntryCatalog.CreateEntries)
        // Locked targets are never resolved (the assignment itself survives in the save, but using it cannot enter build mode)
        public bool TryResolve(Guid id, out PlacementTargetEntry entry)
        {
            var showAllPlaceable = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);
            foreach (var candidate in _catalog.UnlockedEntries(_gameUnlockStateData, showAllPlaceable, _blueprintLibrary.BlueprintEntries))
            {
                if (candidate.Id != id) continue;
                entry = candidate;
                return true;
            }

            entry = default;
            return false;
        }
    }
}
