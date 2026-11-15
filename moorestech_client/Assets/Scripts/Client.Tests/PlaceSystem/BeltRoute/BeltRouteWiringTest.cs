using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Tests.PlaceSystem.Ground.Entry;
using Core.Master;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 実入力と実カメラで2クリック設置を通し、始点固定→経路プレビュー→送信→解除の流れを確かめる
    // Drives two-click placement with real input and camera: start fixed, route preview, send, and cancel
    public class BeltRouteWiringTest : FootprintGroundedEntryFixture
    {
        private static readonly Vector3Int StartCell = new(501, 21, 600);
        private static readonly Vector3Int EndCell = new(505, 21, 600);

        public override void Setup()
        {
            base.Setup();
            // 平らな足場（上面21.2 → セル21）にして経路を素直に引けるようにする
            // Flatten the scaffold (top 21.2 -> cell 21) so routes are straightforward
            Ground.transform.SetPositionAndRotation(new Vector3(504, 20.7f, 600.5f), Quaternion.identity);
            Ground.GetComponent<BoxCollider>().size = new Vector3(40, 1, 40);
            Physics.SyncTransforms();
        }

        // 送信はSE・Undo履歴のゲーム寿命オブジェクトを要するためEditModeでは通さず、録画シナリオで確かめる
        // Sending needs game-lifetime objects (sound, undo history), so it is verified by the recorded scenario rather than in EditMode
        [Test]
        public void StartClickThenAimPreviewsAGreenRouteToTheCursor()
        {
            var system = CreateSystem(out var target);
            var feedback = new PlacementFeedback();

            // 始点候補は1個のゴースト
            // The start candidate is a single ghost
            system.ManualUpdate(new PlaceSystemUpdateContext(target, true, feedback));
            Assert.AreEqual(1, Preview.Cells.Count);
            Assert.AreEqual(StartCell, Preview.Cells[0].Position);
            Assert.IsTrue(Preview.Cells[0].Placeable);

            ClickPress();
            system.ManualUpdate(new PlaceSystemUpdateContext(target, false, feedback));
            ReleaseClick();
            InputSystem.Update();

            // 終点へ照準すると経路が緑で出る
            // Aiming at the end shows the route in green
            PlacementCamera.transform.position = new Vector3(505.5f, 40, 600.5f);
            feedback.Clear();
            system.ManualUpdate(new PlaceSystemUpdateContext(target, false, feedback));
            Assert.IsTrue(Preview.IsActive);
            Assert.GreaterOrEqual(Preview.Cells.Count, 5);
            Assert.AreEqual(StartCell, Preview.Cells[0].Position);
            Assert.AreEqual(EndCell, Preview.Cells[^1].Position);
            Assert.IsTrue(Preview.Cells.All(cell => cell.Placeable));
            Assert.AreEqual(0, feedback.Lines.Count, string.Join(" / ", feedback.Lines.Select(line => line.Key.Key)));
            AssertNoRequest();
        }

        [Test]
        public void CancelDropsOnlyTheStart()
        {
            var system = CreateSystem(out var target);
            var feedback = new PlacementFeedback();
            system.ManualUpdate(new PlaceSystemUpdateContext(target, true, feedback));
            Assert.IsFalse(system.TryCancelInProgressOperation());

            ClickPress();
            system.ManualUpdate(new PlaceSystemUpdateContext(target, false, feedback));
            Assert.IsTrue(system.TryCancelInProgressOperation());
            Assert.IsFalse(system.TryCancelInProgressOperation());
            AssertNoRequest();
        }

        private BeltConveyorPlaceSystem CreateSystem(out BlockPlacementTarget target)
        {
            var system = new BeltConveyorPlaceSystem(PlacementCamera, Preview, Blocks, Inventory, Wallet, new PlacementHeightOffset());
            // 建設コストの無いテスト用ベルト（坂なしファミリー）で送信まで通す
            // A cost-free test belt (a family without slopes) lets the flow reach the send
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId);
            target = new BlockPlacementTarget(master.BlockGuid, null);
            system.Enable();
            return system;
        }
    }
}
