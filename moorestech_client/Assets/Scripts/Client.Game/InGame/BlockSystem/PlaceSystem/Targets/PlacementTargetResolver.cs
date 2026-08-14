using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Common.Debug;
using Game.PlacementTarget;
using Game.UnlockState;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    /// <summary>
    ///     解放済みの設置対象を引く唯一の解決点。IDまたはマスタ表示名から完成した設置対象を返す
    ///     The single resolution point for unlocked placement targets, returning a finished target by id or master display name
    /// </summary>
    public class PlacementTargetResolver
    {
        private readonly PlacementTargetCatalog _catalog;
        private readonly ClientBlueprintLibrary _blueprintLibrary;
        private readonly IGameUnlockStateData _gameUnlockStateData;

        public PlacementTargetResolver(PlacementTargetCatalog catalog, ClientBlueprintLibrary blueprintLibrary, IGameUnlockStateData gameUnlockStateData)
        {
            _catalog = catalog;
            _blueprintLibrary = blueprintLibrary;
            _gameUnlockStateData = gameUnlockStateData;
        }

        // 未解放対象は解決しない（割当自体はセーブに残るが、使用時は建築モードに入れない）
        // Locked targets are never resolved (the assignment itself survives in the save, but using it cannot enter build mode)
        public bool TryResolve(Guid id, out IPlacementTarget target)
        {
            foreach (var entry in UnlockedEntries())
            {
                if (entry.Id != id) continue;
                target = PlacementTargetFactory.Create(entry);
                return true;
            }

            target = null;
            return false;
        }

        // 表示名一致はプレイテストDSL専用の入口。ロケール非依存のマスタ表示名で引く
        // Display-name lookup is the playtest DSL's entry point, matching the locale-independent master display name
        public bool TryResolveByDisplayName(string masterDisplayName, out IPlacementTarget target)
        {
            foreach (var entry in UnlockedEntries())
            {
                if (entry.MasterDisplayName != masterDisplayName) continue;
                target = PlacementTargetFactory.Create(entry);
                return true;
            }

            target = null;
            return false;
        }

        // ビルドメニュー(BuildMenuEntryCatalog.CreateEntries)と同じ供給源を全解決が共有する
        // Every resolution shares the same supply source the build menu uses (BuildMenuEntryCatalog.CreateEntries)
        private IEnumerable<PlacementTargetEntry> UnlockedEntries()
        {
            var showAllPlaceable = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);
            return _catalog.UnlockedEntries(_gameUnlockStateData, showAllPlaceable, _blueprintLibrary.BlueprintEntries);
        }
    }
}
