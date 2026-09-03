using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Mining;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.ProgressBar;
using Client.Input;
using Client.Localization;
using Client.Tests.Common;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Client.Tests.Mining
{
    public class MiningEquipmentSwitchTest : InputTestFixture
    {
        // ForUnitTestの採掘可能mapObjectと許可ツールを識別する
        // Identify the ForUnitTest minable mapObject and its allowed tool
        private static readonly Guid MiningRockGuid = new("00000000-0000-2222-0000-000000000001");
        private static readonly Guid MiningToolItemGuid = new("00000000-0000-0000-1234-000000000001");

        private GameObject _playerObject;
        private GameObject _progressBarObject;
        private GameObject _mapObjectObject;
        private MiningCompleteSoundEffectFixture _soundEffectFixture;
        private Keyboard _keyboard;
        public override void Setup()
        {
            base.Setup();
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _keyboard = InputSystem.AddDevice<Keyboard>();
            TestReflection.ResetInputManagerCache();

            // 取得アイテム名の解決に辞書が要る
            // Focusing resolves earned item names, so stand up the dictionary that RuntimeInitializeOnLoadMethod provides at runtime
            Localize.Initialize();
            CreatePlayerSystem();
            CreateProgressBarView();
            _soundEffectFixture = new MiningCompleteSoundEffectFixture();
            _mapObjectObject = new GameObject("MiningMapObjects");
            #region Internal
            void CreatePlayerSystem()
            {
                _playerObject = new GameObject("PlayerSystem");
                var grabItemManager = _playerObject.AddComponent<PlayerGrabItemManager>();
                var playerController = _playerObject.AddComponent<PlayerObjectController>();
                TestReflection.SetField(playerController, "animator", _playerObject.AddComponent<Animator>());
                var container = _playerObject.AddComponent<PlayerSystemContainer>();
                TestReflection.SetField(container, "playerGrabItemManager", grabItemManager);
                TestReflection.SetField(container, "playerObjectController", playerController);
                TestReflection.InvokePrivate(container, "Awake");
            }
            void CreateProgressBarView()
            {
                _progressBarObject = new GameObject("ProgressBarView");
                var view = _progressBarObject.AddComponent<ProgressBarView>();
                var viewRoot = new GameObject("ViewRoot");
                viewRoot.transform.SetParent(_progressBarObject.transform);
                TestReflection.SetField(view, "viewRoot", viewRoot);
                TestReflection.SetField(view, "scrollbar", _progressBarObject.AddComponent<Scrollbar>());
                TestReflection.InvokePrivate(view, "Awake");
            }

            #endregion
        }

        public override void TearDown()
        {
            ProgressBarView.Instance = null;
            TestReflection.SetStaticProperty(typeof(PlayerSystemContainer), "Instance", null);
            UnityEngine.Object.DestroyImmediate(_mapObjectObject);
            _soundEffectFixture.Destroy();
            UnityEngine.Object.DestroyImmediate(_progressBarObject);
            UnityEngine.Object.DestroyImmediate(_playerObject);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }
        [Test]
        public void 採掘中に装備を持ち替えるとフォーカス状態へ戻る()
        {
            var context = new MiningControllerContext(CreateEquipmentHoldingTool());
            context.SetFocusTarget(CreateMiningMapObject());
            var miningState = new MiningProgressState(context.CurrentFocusTarget, MiningToolOfFocusedMapObject(context));
            PressInteract();
            // 装備が変わらない限り採掘は継続する（この土台が無いと切替検知の失敗を検出できない）
            // Mining continues while the equipment is unchanged; without this baseline a broken switch check is invisible
            Assert.AreSame(miningState, miningState.GetNextUpdate(context, 0.01f));
            // サーバーは現在の装備でGUID照合するため、空スロットへ持ち替えた時点で進捗を進めてはいけない
            // The server matches the GUID of the current equipment, so progress must stop the moment an empty slot is selected
            context.LocalPlayerEquipment.ApplySelected(1);
            Assert.IsInstanceOf<MiningFocusState>(miningState.GetNextUpdate(context, 0.01f));
        }
        [Test]
        public void 完了後に照準対象が変わっても開始対象だけを攻撃する()
        {
            var context = new MiningControllerContext(CreateEquipmentHoldingTool());
            var startedTarget = new AttackTrackingMiningTarget("StartedTarget", _mapObjectObject.transform);
            var replacementTarget = new AttackTrackingMiningTarget("ReplacementTarget", _mapObjectObject.transform);
            context.SetFocusTarget(startedTarget);
            var miningTool = new MiningToolCandidate(context.LocalPlayerEquipment.SelectedItem.Id, 0.01f);
            var miningState = new MiningProgressState(startedTarget, miningTool);
            PressInteract();
            var completeState = miningState.GetNextUpdate(context, miningTool.AttackSpeed);
            Assert.IsInstanceOf<MiningCompleteState>(completeState);

            // 完了後の照準変更でも開始対象だけへ送信する
            // Send only to the started target even if focus changes after completion
            context.SetFocusTarget(replacementTarget);
            completeState.GetNextUpdate(context, 0);
            Assert.AreEqual(1, startedTarget.AttackCallCount);
            Assert.AreEqual(0, replacementTarget.AttackCallCount);
        }

        [Test]
        public void 採掘中に照準対象が変わるとフォーカス状態へ戻る()
        {
            var context = new MiningControllerContext(CreateEquipmentHoldingTool());
            context.SetFocusTarget(CreateMiningMapObject());
            var miningState = new MiningProgressState(context.CurrentFocusTarget, MiningToolOfFocusedMapObject(context));
            PressInteract();

            context.SetFocusTarget(CreateMiningMapObject());

            Assert.IsInstanceOf<MiningFocusState>(miningState.GetNextUpdate(context, 0.01f));
        }

        private LocalPlayerEquipment CreateEquipmentHoldingTool()
        {
            var toolItemId = MasterHolder.ItemMaster.GetItemId(MiningToolItemGuid);
            var equipment = new LocalPlayerEquipment();
            equipment.Initialize(new List<IItemStack> { ServerContext.ItemStackFactory.Create(toolItemId, 1) }, 0);
            return equipment;
        }

        private MapObjectGameObject CreateMiningMapObject()
        {
            var mapObjectObject = new GameObject("MiningMapObject");
            mapObjectObject.transform.SetParent(_mapObjectObject.transform);
            var mapObject = mapObjectObject.AddComponent<MapObjectGameObject>();
            mapObject.SetRuntimeIdentity(1, MiningRockGuid.ToString());
            mapObject.Initialize(new GetMapObjectInfoProtocol.MapObjectsInfoMessagePack(1, false, 30));
            return mapObject;
        }

        private MiningToolCandidate MiningToolOfFocusedMapObject(MiningControllerContext context)
        {
            var target = context.CurrentFocusTarget;
            var equippedItemId = context.LocalPlayerEquipment.SelectedItem.Id;
            Assert.AreEqual(MiningStartOutcome.Ready, target.TryBeginHandMining(equippedItemId, out var miningTool, out _));
            return miningTool;
        }

        private void PressInteract()
        {
            // 入力アセットの生成(Enable)を状態イベントより先に済ませないとバインドが解決されない
            // The input asset must be created (and enabled) before the state event, otherwise its bindings never resolve
            var interact = InputManager.Playable.Interact;
            InputSystem.Update();
            Press(_keyboard.fKey);
            InputSystem.Update();
            // 押下が届いていないと全遷移がフォーカス復帰に化けてテストが無意味になるため前提を固定する
            // Without the press landing every transition collapses into a focus fallback and the test proves nothing
            Assert.IsTrue(interact.GetKey, "Fの押下がInputSystemへ届いていない");
        }

    }
}
