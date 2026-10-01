using System.Runtime.Serialization;
using Client.Game.InGame.Block;
using Client.Game.InGame.Interact.Selection;
using Client.Game.InGame.Interact.Tap;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.CancelInput;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Tests.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Interact.Reach
{
    /// <summary>
    ///     SubInventoryStateが毎フレームの到達判定で閉じる配線を、Fで開いた実ソースで検証
    ///     Verifies SubInventoryState's per-frame reach close wiring with the real source produced by the F-open action
    /// </summary>
    public class SubInventoryReachCloseTest : InteractTargetSelectorTestFixture
    {
        [SetUp]
        public void AddKeyboard()
        {
            InputSystem.AddDevice<Keyboard>();
        }

        [Test]
        public void 届く位置では閉じず自機が離れるとGameScreenへ遷移する()
        {
            var block = CreateOpenableBlockTarget(new Vector3(1f, 0f, 0f));
            var state = CreateOpenedState(block);

            PlayerObject.transform.position = Vector3.zero;
            Assert.IsNull(state.GetNextUpdate(), "届く位置では閉じない");

            PlayerObject.transform.position = new Vector3(-InteractOverlap.InteractDistance - 1f, 0f, 0f);
            Assert.AreEqual(UIStateEnum.GameScreen, state.GetNextUpdate()?.NextStateEnum);
        }

        [Test]
        public void 距離内でも対象が対話不能になるとGameScreenへ遷移する()
        {
            var block = CreateOpenableBlockTarget(new Vector3(1f, 0f, 0f));
            var state = CreateOpenedState(block);
            PlayerObject.transform.position = Vector3.zero;
            Assert.IsNull(state.GetNextUpdate());

            block.GetComponent<BlockGameObject>().MarkUnsearchable();

            Assert.AreEqual(UIStateEnum.GameScreen, state.GetNextUpdate()?.NextStateEnum);
        }

        [Test]
        public void 距離内でもブロックの表示が破棄されると例外なしでGameScreenへ遷移する()
        {
            var block = CreateOpenableBlockTarget(new Vector3(1f, 0f, 0f));
            var state = CreateOpenedState(block);
            PlayerObject.transform.position = Vector3.zero;
            Assert.IsNull(state.GetNextUpdate());

            // TearDownが二重に破棄しないよう後始末の対象から外してから壊す
            // Drop it from the cleanup list first so TearDown never destroys it twice
            TargetObjects.Remove(block.gameObject);
            Object.DestroyImmediate(block.gameObject);
            Physics.SyncTransforms();

            Assert.AreEqual(UIStateEnum.GameScreen, state.GetNextUpdate()?.NextStateEnum);
        }

        [Test]
        public void 車両の表示が同じIDで作り直されても届く位置なら閉じない()
        {
            var openedCar = CreateTrainCarTarget(new Vector3(1f, 0f, 0f));
            var openedEntity = openedCar.GetComponent<TrainCarEntityObject>();
            var datastore = openedCar.GetComponent<TrainCarObjectDatastore>();
            var state = CreateOpenedState(openedCar);
            PlayerObject.transform.position = Vector3.zero;

            // 同じ位置・同じIDで新しいviewが登録され、開いた瞬間の旧viewは手の届かない所へ退く
            // A new view is registered at the same place under the same ID while the view held at open moves out of reach
            var rebuiltCar = CreateTrainCarTarget(new Vector3(1f, 0f, 0f));
            TrainCarObjectDatastoreTestUtil.Register(datastore, openedEntity.TrainCarInstanceId, rebuiltCar.GetComponent<TrainCarEntityObject>());
            openedCar.transform.position = new Vector3(50f, 0f, 0f);
            Physics.SyncTransforms();

            Assert.IsNull(state.GetNextUpdate(), "作り直しただけで一歩も離れていないのに閉じた");
        }

        [Test]
        public void 車両の表示が登録から消えるとGameScreenへ遷移する()
        {
            var car = CreateTrainCarTarget(new Vector3(1f, 0f, 0f));
            var state = CreateOpenedState(car);
            PlayerObject.transform.position = Vector3.zero;
            Assert.IsNull(state.GetNextUpdate());

            TrainCarObjectDatastoreTestUtil.Unregister(car.GetComponent<TrainCarObjectDatastore>(), car.GetComponent<TrainCarEntityObject>().TrainCarInstanceId);

            Assert.AreEqual(UIStateEnum.GameScreen, state.GetNextUpdate()?.NextStateEnum);
        }

        // コンストラクタはサーバー購読を伴うため、GetNextUpdateが読む依存とFで開いた実ソースだけを差し込む
        // The constructor subscribes to the server, so inject only what GetNextUpdate reads plus the real source from the F-open action
        private static SubInventoryState CreateOpenedState(ITapInteractable openedTarget)
        {
            var source = openedTarget.Actions[0].Execute().TransitContext.GetContext<ISubInventorySource>();
            var state = (SubInventoryState)FormatterServices.GetUninitializedObject(typeof(SubInventoryState));
            TestReflection.SetField(state, "_outOfReachDetector", new SubInventoryOutOfReachDetector());
            TestReflection.SetField(state, "_rightShortPressInputService", new RightShortPressInputService(new RightShortPressInput()));
            TestReflection.SetField(state, "<CurrentSubInventorySource>k__BackingField", source);
            return state;
        }
    }
}
