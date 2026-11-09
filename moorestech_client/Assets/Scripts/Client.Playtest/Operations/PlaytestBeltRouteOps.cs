using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Context;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     ベルトの2クリック設置に固有の操作。向きの回転と、Ctrl+クリックによる分岐の始点決め
    ///     Operations specific to two-click belt placement: rotating the facing and fixing a branch start with Ctrl+click
    /// </summary>
    public static class PlaytestBeltRouteOps
    {
        // 始点クリック→終点クリックで最短経路が置かれる。選択直後の向きは北なので、そこからRで合わせる
        // A start click then an end click places the shortest route; the facing right after selection is north, so R aligns from there
        public static async UniTask RoutePlaceViaUi(PlaytestDriver driver, string blockName, Vector3Int fromOrigin, BlockDirection startDirection, Vector3Int toOrigin, BlockDirection endDirection)
        {
            await driver.OpenBuildMenuAndSelectBlock(blockName);
            driver.Note($"経路設置: {blockName} {fromOrigin}:{startDirection} -> {toOrigin}:{endDirection}");
            await driver.AimAt(PlaytestUiOps.PlaceAimPoint(blockName, fromOrigin, BlockDirection.North));
            await RotateTo(driver, BlockDirection.North, startDirection);
            await driver.ClickPlace();
            await driver.AimAt(PlaytestUiOps.PlaceAimPoint(blockName, toOrigin, BlockDirection.North));
            await RotateTo(driver, startDirection, endDirection);
            await driver.ClickPlace();
            await driver.Until(() => PlaytestBlockOps.GetBlock(fromOrigin) != null && PlaytestBlockOps.GetBlock(toOrigin) != null, 15f, $"UI経路設置反映: {blockName} {fromOrigin}->{toOrigin}");
        }

        // Ctrlを押したまま既存のベルトをクリックして分岐器へ置き換え、exitDirection の出口から終点まで経路を置く
        // Click an existing belt with Ctrl held to replace it with a splitter and route from the exitDirection exit to the end
        // exitDirection は呼び手が配置から決める（直線の既定は右、角の既定は正面。useAlternateExit で残りの出口）
        // The caller derives exitDirection from the layout (a straight belt defaults to right, a corner to front; useAlternateExit picks the other exit)
        public static async UniTask BranchPlaceViaUi(PlaytestDriver driver, string blockName, Vector3Int beltCell, bool useAlternateExit, BlockDirection exitDirection, Vector3Int toOrigin, BlockDirection endDirection)
        {
            await driver.OpenBuildMenuAndSelectBlock(blockName);
            driver.Note($"分岐設置: {blockName} {beltCell} 出口{exitDirection}{(useAlternateExit ? "(R切替)" : "")} -> {toOrigin}:{endDirection}");
            await BranchStart(driver, beltCell, useAlternateExit);
            await driver.AimAt(PlaytestUiOps.PlaceAimPoint(blockName, toOrigin, BlockDirection.North));
            await RotateTo(driver, exitDirection, endDirection);
            await driver.ClickPlace();
            // 置かれただけでなく終点の向きまで見て、出口の取り違えを黙って通さない
            // Check the end facing, not just presence, so a mixed-up exit fails instead of passing silently
            await driver.Until(() => PlaytestBlockOps.GetBlock(toOrigin) != null && ServerContext.WorldBlockDatastore.GetBlockDirection(toOrigin) == endDirection, 15f,
                $"UI分岐設置反映: {blockName} {beltCell}->{toOrigin}:{endDirection}（向きが違えば exitDirection={exitDirection}/useAlternateExit={useAlternateExit} の取り違え）");
        }

        // Rは時計回りに90度ずつ回す。現在の向きから目標までの押下回数を求めて注入する
        // R turns 90 degrees clockwise each press; inject as many presses as it takes from the current facing to the target
        public static async UniTask RotateTo(PlaytestDriver driver, BlockDirection current, BlockDirection target)
        {
            var presses = (((int)target - (int)current) % 4 + 4) % 4;
            for (var i = 0; i < presses; i++) await driver.PressKey(Key.R);
        }

        // 既存ベルトをCtrl+クリックして分岐の始点にする。始点確定後の向きは出口の向きになる（終点の向き回転の起点）
        // Fixes a branch start by Ctrl+clicking an existing belt; the facing after confirmation is the exit heading, the base for rotating the end facing
        private static async UniTask BranchStart(PlaytestDriver driver, Vector3Int beltCell, bool useAlternateExit)
        {
            // ベルトの箱の内側を狙えば、カメラからのレイは必ずその境界箱の面に当たる
            // Aiming inside the belt's box guarantees the camera ray hits that bounding box's face first
            SemanticInput.KeyDown(Key.LeftCtrl);
            await driver.AimAt(new Vector3(beltCell.x + 0.5f, beltCell.y + 0.5f, beltCell.z + 0.5f));
            // 既定の出口は直線なら右、角なら正面。R で残りの出口へ切り替える
            // The default exit is right for a straight belt and front for a corner; R switches to the other exit
            if (useAlternateExit) await driver.PressKey(Key.R);
            await driver.ClickPlace();
            SemanticInput.KeyUp(Key.LeftCtrl);
            await UniTask.DelayFrame(2);
        }
    }
}
