using System.Runtime.Serialization;
using Client.Game.InGame.Block;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Interact.Selection;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.CancelInput;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Network.API;
using Client.Tests.Common;
using NUnit.Framework;
using Server.Util.MessagePack;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Interact.Reach
{
    /// <summary>
    ///     SubInventoryStateが毎フレームの到達判定で閉じる配線を検証。距離内なら留まり、自機が離れるか対象が対話不能になると閉じる
    ///     Verifies SubInventoryState wires the per-frame reach check: it stays while within reach and closes once the player leaves or the target stops being interactable
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

            PlayerObject.transform.position = new Vector3(-InteractTargetSelector.InteractDistance - 1f, 0f, 0f);
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
        public void 距離内でも対象が破棄されると例外なしでGameScreenへ遷移する()
        {
            var block = CreateOpenableBlockTarget(new Vector3(1f, 0f, 0f));
            var state = CreateOpenedState(block);
            PlayerObject.transform.position = Vector3.zero;
            Assert.IsNull(state.GetNextUpdate());

            TargetObjects.Remove(block.gameObject);
            Object.DestroyImmediate(block.gameObject);
            Physics.SyncTransforms();

            Assert.AreEqual(UIStateEnum.GameScreen, state.GetNextUpdate()?.NextStateEnum);
        }

        #region Internal

        // コンストラクタはサーバー購読を伴うため、GetNextUpdateが読む依存だけを直接差し込む
        // The constructor subscribes to the server, so only the dependencies GetNextUpdate reads are injected directly
        private static SubInventoryState CreateOpenedState(IInteractable reachTarget)
        {
            var state = (SubInventoryState)FormatterServices.GetUninitializedObject(typeof(SubInventoryState));
            TestReflection.SetField(state, "_reachQuery", new InteractReachQuery());
            TestReflection.SetField(state, "_rightShortPressInputService", new RightShortPressInputService(new RightShortPressInput()));
            TestReflection.SetField(state, "<CurrentSubInventorySource>k__BackingField", new FakeReachSource(reachTarget));
            return state;
        }

        private class FakeReachSource : ISubInventorySource
        {
            public FakeReachSource(IInteractable reachTarget)
            {
                ReachTarget = reachTarget;
            }

            public InventoryIdentifierMessagePack InventoryIdentifier => InventoryIdentifierMessagePack.CreateBlockMessage(Vector3Int.zero);
            public IInteractable ReachTarget { get; }
            public SubInventoryModel CreateModel(InventoryResponse inventoryResponse) => null;
        }

        #endregion
    }
}
