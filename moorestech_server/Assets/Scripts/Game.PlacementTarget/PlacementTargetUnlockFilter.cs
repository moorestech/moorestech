using System;
using Game.UnlockState;

namespace Game.PlacementTarget
{
    // ビルドメニューの表示可否判定をuGUI/web両カタログで共有する（複製すると片方の更新漏れで未解放ブロックが設置可能になる）
    // Shares the build-menu visibility decision between the uGUI and web catalogs (duplicating it lets a missed update expose locked blocks for placement)
    public static class PlacementTargetUnlockFilter
    {
        public static bool IsUnlocked(PlacementTargetEntry entry, IGameUnlockStateData unlockState, bool showAllPlaceable)
        {
            switch (entry.Kind)
            {
                case PlacementTargetKind.Block:
                    return showAllPlaceable || (unlockState.BlockUnlockStateInfos.TryGetValue(entry.Id, out var blockInfo) && blockInfo.IsUnlocked);
                case PlacementTargetKind.TrainCar:
                    return showAllPlaceable || (unlockState.TrainCarUnlockStateInfos.TryGetValue(entry.Id, out var trainCarInfo) && trainCarInfo.IsUnlocked);
                case PlacementTargetKind.ConnectTool:
                    // 接続ツールだけはshowAllPlaceableを見ない（集約前からの意図的な非対称を挙動不変で保存している）
                    // Connect tools alone ignore showAllPlaceable, preserving the intentional asymmetry that existed before this consolidation
                    return unlockState.ConnectToolUnlockStateInfos.TryGetValue(entry.Id, out var connectToolInfo) && connectToolInfo.IsUnlocked;
                case PlacementTargetKind.BuildTool:
                case PlacementTargetKind.Blueprint:
                    // ビルドツールとBPは解放条件を持たず常に表示する
                    // Build tools and blueprints have no unlock condition and are always shown
                    return true;
                default:
                    // 未知のKindは型で排除する到達不能ケース
                    // Unreachable: unknown Kind is excluded by the type
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
