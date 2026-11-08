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

        // Ctrlを押したまま既存の直線ベルトをクリックして分岐器へ置き換え、横の出口から終点まで経路を置く
        // Click an existing straight belt with Ctrl held to replace it with a splitter and route from the side exit to the end
        public static async UniTask BranchPlaceViaUi(PlaytestDriver driver, string blockName, Vector3Int beltCell, bool sideIsRight, Vector3Int toOrigin, BlockDirection endDirection)
        {
            await driver.OpenBuildMenuAndSelectBlock(blockName);
            driver.Note($"分岐設置: {blockName} {beltCell} {(sideIsRight ? "右" : "左")} -> {toOrigin}:{endDirection}");
            var sideDirection = await BranchStart(driver, beltCell, sideIsRight);
            await driver.AimAt(PlaytestUiOps.PlaceAimPoint(blockName, toOrigin, BlockDirection.North));
            await RotateTo(driver, sideDirection, endDirection);
            await driver.ClickPlace();
            await driver.Until(() => PlaytestBlockOps.GetBlock(toOrigin) != null, 15f, $"UI分岐設置反映: {blockName} {beltCell}->{toOrigin}");
        }

        // Rは時計回りに90度ずつ回す。現在の向きから目標までの押下回数を求めて注入する
        // R turns 90 degrees clockwise each press; inject as many presses as it takes from the current facing to the target
        public static async UniTask RotateTo(PlaytestDriver driver, BlockDirection current, BlockDirection target)
        {
            var presses = (((int)target - (int)current) % 4 + 4) % 4;
            for (var i = 0; i < presses; i++) await driver.PressKey(Key.R);
        }

        // 既存ベルトをCtrl+クリックして分岐の始点にする。戻り値は経路が出る横の出口の向き（終点の向き回転の起点）
        // Fixes a branch start by Ctrl+clicking an existing belt; returns the side exit heading, the base for rotating the end facing
        public static async UniTask<BlockDirection> BranchStart(PlaytestDriver driver, Vector3Int beltCell, bool sideIsRight)
        {
            var facing = ServerContext.WorldBlockDatastore.GetBlockDirection(beltCell);
            var side = sideIsRight ? facing.HorizonRotation() : facing.HorizonRotation().HorizonRotation().HorizonRotation();

            // ベルトの箱の内側を狙えば、カメラからのレイは必ずその境界箱の面に当たる
            // Aiming inside the belt's box guarantees the camera ray hits that bounding box's face first
            SemanticInput.KeyDown(Key.LeftCtrl);
            await driver.AimAt(new Vector3(beltCell.x + 0.5f, beltCell.y + 0.5f, beltCell.z + 0.5f));
            // 分岐の既定は右。左はRで切り替える
            // The default side is right; R switches to left
            if (!sideIsRight) await driver.PressKey(Key.R);
            await driver.ClickPlace();
            SemanticInput.KeyUp(Key.LeftCtrl);
            await UniTask.DelayFrame(2);
            return side;
        }
    }
}
