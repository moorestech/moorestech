using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using UnityEngine;
using Game.PlacementTarget;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public static class PlacementTargetFactory
    {
        // カタログエントリからIPlacementTargetを生成する唯一の解決点
        // The single resolution point from catalog entry to IPlacementTarget
        public static bool TryCreate(PlacementTargetEntry entry, IBlueprintLookup blueprintLibrary, out IPlacementTarget target)
        {
            switch (entry.Kind)
            {
                case PlacementTargetKind.Block:
                    target = new BlockPlacementTarget(entry.Id, null);
                    return true;
                case PlacementTargetKind.TrainCar:
                    target = new TrainCarPlacementTarget(entry.Id);
                    return true;
                case PlacementTargetKind.ConnectTool:
                    target = new ConnectToolPlacementTarget(entry.Id);
                    return true;
                case PlacementTargetKind.BlueprintCopy:
                    target = new BlueprintCopyPlacementTarget(entry.Id);
                    return true;
                case PlacementTargetKind.Blueprint:
                    // 一覧と本体の同期ずれを表示対象へ持ち込まない
                    // Exclude entries whose body has not synchronized with the list
                    if (!blueprintLibrary.TryGetBlueprint(entry.Id, out var blueprint))
                    {
                        Debug.LogWarning($"[PlacementTargetFactory] blueprint {entry.Id} has no synchronized body; target omitted");
                        target = null;
                        return false;
                    }
                    target = new BlueprintPlacementTarget(entry.Id, entry.MasterDisplayName, blueprint);
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry.Kind), entry.Kind, null);
            }
        }
    }
}
