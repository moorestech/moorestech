using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Common.Debug;
using Game.PlacementTarget;
using Game.UnlockState;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    /// <summary>
    ///     解放済みの設置対象を引く唯一の解決点。IDから完成した設置対象を返し、解放済み一覧も供給する
    ///     The single resolution point for unlocked placement targets: resolves one by id and supplies the unlocked list
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

        // 未解放対象は解決せず建築不可のまま残す
        // Locked targets stay unresolved and unusable, though the assignment persists
        // Idはカタログ内で一意のため最初の一致で足りる（重複や全件収集は考慮不要）
        // Ids are unique within the catalog, so the first match suffices
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

        // 解放済み対象を完成形で列挙する。ビルドメニューのuGUI版・Web版が共有する唯一の供給点
        // Enumerates the unlocked targets as finished objects; the single supply point shared by the uGUI and web build menus
        public IReadOnlyList<IPlacementTarget> CreateUnlockedTargets()
        {
            var targets = new List<IPlacementTarget>();
            foreach (var entry in UnlockedEntries())
            {
                targets.Add(PlacementTargetFactory.Create(entry));
            }

            return targets;
        }

        // 解放判定と無料設置デバッグを一箇所で解決する。呼び出し側は解放条件を再実装しない
        // Resolves the unlock check and the free-placement debug flag in one place, so callers never re-implement the condition
        public IEnumerable<PlacementTargetEntry> UnlockedEntries()
        {
            var showAllPlaceable = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);
            return _catalog.UnlockedEntries(_gameUnlockStateData, showAllPlaceable, _blueprintLibrary.BlueprintEntries);
        }
    }
}
