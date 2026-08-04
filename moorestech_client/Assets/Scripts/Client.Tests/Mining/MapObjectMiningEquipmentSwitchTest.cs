using System;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Mining;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.ProgressBar;
using Client.Input;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Client.Tests.Mining
{
    /// <summary>
    ///     採掘中の装備切替がクライアント進捗とサーバー判定を乖離させないことを検証する
    ///     Verifies that switching equipment mid-mining never lets the client progress diverge from the server's check
    /// </summary>
    public class MapObjectMiningEquipmentSwitchTest : InputTestFixture
    {
        // ForUnitTest master の Mining 型 mapObject と、そのminingToolsに載っているツールアイテム
        // The Mining-type mapObject in the ForUnitTest master, plus the tool item listed in its miningTools
        private static readonly Guid MiningRockGuid = new("00000000-0000-2222-0000-000000000001");
        private static readonly Guid MiningToolItemGuid = new("00000000-0000-0000-1234-000000000001");

        private GameObject _playerObject;
        private GameObject _progressBarObject;
        private GameObject _mapObjectObject;
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _mouse = InputSystem.AddDevice<Mouse>();
            ResetInputManagerCache();
            CreatePlayerSystem();
            CreateProgressBarView();

            #region Internal

            void CreatePlayerSystem()
            {
                _playerObject = new GameObject("PlayerSystem");
                var grabItemManager = _playerObject.AddComponent<PlayerGrabItemManager>();
                var playerController = _playerObject.AddComponent<PlayerObjectController>();
                SetField(playerController, "animator", _playerObject.AddComponent<Animator>());
                var container = _playerObject.AddComponent<PlayerSystemContainer>();
                SetField(container, "playerGrabItemManager", grabItemManager);
                SetField(container, "playerObjectController", playerController);
                InvokePrivate(container, "Awake");
            }

            void CreateProgressBarView()
            {
                _progressBarObject = new GameObject("ProgressBarView");
                var view = _progressBarObject.AddComponent<ProgressBarView>();
                var viewRoot = new GameObject("ViewRoot");
                viewRoot.transform.SetParent(_progressBarObject.transform);
                SetField(view, "viewRoot", viewRoot);
                SetField(view, "scrollbar", _progressBarObject.AddComponent<Scrollbar>());
                InvokePrivate(view, "Awake");
            }

            #endregion
        }

        public override void TearDown()
        {
            ProgressBarView.Instance = null;
            SetStaticProperty(typeof(PlayerSystemContainer), "Instance", null);
            UnityEngine.Object.DestroyImmediate(_mapObjectObject);
            UnityEngine.Object.DestroyImmediate(_progressBarObject);
            UnityEngine.Object.DestroyImmediate(_playerObject);
            ResetInputManagerCache();
            base.TearDown();

            #region Internal

            static void SetStaticProperty(Type targetType, string propertyName, object value)
            {
                var field = targetType.GetField($"<{propertyName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
                field.SetValue(null, value);
            }

            #endregion
        }

        [Test]
        public void 採掘中に装備を持ち替えるとフォーカス状態へ戻る()
        {
            var context = new MapObjectMiningControllerContext(CreateEquipmentHoldingTool());
            context.SetFocusTarget(CreateMiningMapObject());
            var miningState = new MapObjectMiningMiningState(MiningToolOfFocusedMapObject(context), context.LocalPlayerEquipment.SelectedItem.Id);
            PressLeftClick();

            // 装備が変わらない限り採掘は継続する（この土台が無いと切替検知の失敗を検出できない）
            // Mining continues while the equipment is unchanged; without this baseline a broken switch check is invisible
            Assert.AreSame(miningState, miningState.GetNextUpdate(context, 0.01f));

            // サーバーは現在の装備でGUID照合するため、素手へ持ち替えた時点で進捗を進めてはいけない
            // The server matches the GUID of the current equipment, so progress must stop the moment bare hands are selected
            context.LocalPlayerEquipment.ApplySelected(IEquipmentInventory.BareHandsIndex);
            Assert.IsInstanceOf<MapObjectMiningFocusState>(miningState.GetNextUpdate(context, 0.01f));
        }

        [Test]
        public void 装備が採掘ツールのままなら採掘完了まで進む()
        {
            var context = new MapObjectMiningControllerContext(CreateEquipmentHoldingTool());
            context.SetFocusTarget(CreateMiningMapObject());
            var miningTool = MiningToolOfFocusedMapObject(context);
            var miningState = new MapObjectMiningMiningState(miningTool, context.LocalPlayerEquipment.SelectedItem.Id);
            PressLeftClick();

            // 切替検知が誤爆すると完了へ到達できなくなるため、同一装備での完走も固定する
            // A false positive in the switch check would block completion, so the same-equipment run is pinned too
            Assert.IsInstanceOf<MapObjectMiningMiningCompleteState>(miningState.GetNextUpdate(context, miningTool.AttackSpeed));
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
            _mapObjectObject = new GameObject("MiningMapObject");
            var mapObject = _mapObjectObject.AddComponent<MapObjectGameObject>();
            mapObject.SetRuntimeIdentity(1, MiningRockGuid.ToString());
            mapObject.Initialize(new GetMapObjectInfoProtocol.MapObjectsInfoMessagePack(1, false, 30));
            return mapObject;
        }

        private MiningToolCandidate MiningToolOfFocusedMapObject(MapObjectMiningControllerContext context)
        {
            var target = context.CurrentFocusTarget;
            var equippedItemId = context.LocalPlayerEquipment.SelectedItem.Id;
            Assert.IsTrue(target.TryResolveUsableTool(equippedItemId, out var miningTool));
            return miningTool;
        }

        private void PressLeftClick()
        {
            // 入力アセットの生成(Enable)を状態イベントより先に済ませないとバインドが解決されない
            // The input asset must be created (and enabled) before the state event, otherwise its bindings never resolve
            var leftClick = InputManager.Playable.ScreenLeftClick;
            InputSystem.Update();
            Press(_mouse.leftButton);
            InputSystem.Update();
            // 押下が届いていないと全遷移がフォーカス復帰に化けてテストが無意味になるため前提を固定する
            // Without the press landing every transition collapses into a focus fallback and the test proves nothing
            Assert.IsTrue(leftClick.GetKey, "左クリックの押下がInputSystemへ届いていない");
        }

        // InputTestFixtureがInputSystemを差し替えるため、他セッションで作られた入力アセットは捨てて張り直す
        // InputTestFixture swaps the InputSystem, so an input asset built in another session must be dropped and rebuilt
        private static void ResetInputManagerCache()
        {
            foreach (var fieldName in new[] { "_instance", "player", "playable", "ui" })
            {
                typeof(InputManager).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            }
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }
    }
}
