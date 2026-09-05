using System;
using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Mining;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.ProgressBar;
using Client.Game.InGame.UI.Tooltip;
using Client.Input;
using Client.Localization;
using Client.Tests.Common;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Interact
{
    /// <summary>
    ///     他UIステート離脱時の後始末を検証
    ///     Verifies the teardown when leaving for another UI state: the mining FSM is walked down to Idle, never discarded
    /// </summary>
    public class InteractControllerDisableTest : InputTestFixture
    {
        private static readonly Guid ToolItemGuid = new("00000000-0000-0000-1234-000000000001");

        private Keyboard _keyboard;
        private GameObject _playerObject;
        private GameObject _tooltipObject;
        private GameObject _stubTargetObject;
        private ProgressBarState _progressBar;

        public override void Setup()
        {
            base.Setup();
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _keyboard = InputSystem.AddDevice<Keyboard>();
            TestReflection.ResetInputManagerCache();

            // 文言解決は実辞書を通す
            // Resolve text through the real dictionary
            Localize.Initialize();
            CreateTooltip();
            CreatePlayerSystem();
            _progressBar = new ProgressBarState();

            #region Internal

            void CreateTooltip()
            {
                _tooltipObject = new GameObject("MouseCursorTooltip");
                _tooltipObject.SetActive(false);
                var tooltip = _tooltipObject.AddComponent<MouseCursorTooltip>();
                TestReflection.SetField(tooltip, "canvasGroup", _tooltipObject.AddComponent<CanvasGroup>());
                TestReflection.SetField(tooltip, "itemName", _tooltipObject.AddComponent<TextMeshProUGUI>());
                _tooltipObject.SetActive(true);
                TestReflection.InvokePrivate(tooltip, "Awake");
            }

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

            #endregion
        }

        public override void TearDown()
        {
            TestReflection.SetStaticProperty(typeof(PlayerSystemContainer), "Instance", null);
            TestReflection.SetStaticProperty(typeof(MouseCursorTooltip), "Instance", null);
            UnityEngine.Object.DestroyImmediate(_stubTargetObject);
            UnityEngine.Object.DestroyImmediate(_playerObject);
            UnityEngine.Object.DestroyImmediate(_tooltipObject);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void 採掘中にDisableすると進捗バーが消えFSMがIdleへ戻る()
        {
            var selector = new ScriptedInteractTargetSelector();
            var controller = new InteractController(CreateEquipmentHoldingTool(), selector, _progressBar);
            selector.SetNext(CreateReadyMiningTarget());
            PressInteract();

            // Idle→Focus→Progressへ進行
            // Walk up through Idle, Focus and into Progress
            controller.ManualUpdate();
            controller.ManualUpdate();
            Assert.IsInstanceOf<MiningProgressState>(CurrentMiningState(controller));
            Assert.IsTrue(_progressBar.IsShown, "採掘中の進捗バーが出ていない前提が崩れている");

            // ステートを捨てるとProgressの出口処理が飛び、バーとアニメが固着する
            // Discarding the state skips the Progress exit work and strands the bar and the animation
            controller.Disable();
            Assert.IsInstanceOf<MiningIdleState>(CurrentMiningState(controller));
            Assert.IsFalse(_progressBar.IsShown);
        }

        [Test]
        public void フォーカス中にDisableするとtooltipが消える()
        {
            var selector = new ScriptedInteractTargetSelector();
            var controller = new InteractController(CreateEquipmentHoldingTool(), selector, _progressBar);
            selector.SetNext(CreateReadyMiningTarget());

            // Fを押さないのでFocusに留まり、掘り方のtooltipが出る
            // F is never pressed, so it stays in Focus and shows the how-to-mine tooltip
            controller.ManualUpdate();
            controller.ManualUpdate();
            Assert.IsTrue(MouseCursorTooltip.Instance.GetPresentation().Visible, "フォーカス中のtooltipが出ていない前提が崩れている");

            controller.Disable();
            Assert.IsInstanceOf<MiningIdleState>(CurrentMiningState(controller));
            Assert.IsFalse(MouseCursorTooltip.Instance.GetPresentation().Visible);
        }

        private static IMiningState CurrentMiningState(InteractController controller)
        {
            return TestReflection.GetField<IMiningState>(controller, "_miningState");
        }

        private static LocalPlayerEquipment CreateEquipmentHoldingTool()
        {
            var toolItemId = MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
            var equipment = new LocalPlayerEquipment();
            equipment.Initialize(new List<IItemStack> { ServerContext.ItemStackFactory.Create(toolItemId, 1) }, 0);
            return equipment;
        }

        private ReadyMiningTarget CreateReadyMiningTarget()
        {
            var target = new ReadyMiningTarget();
            _stubTargetObject = target.GameObject;
            return target;
        }

        private void PressInteract()
        {
            // 入力アセットの生成(Enable)を状態イベントより先に済ませないとバインドが解決されない
            // The input asset must be created (and enabled) before the state event, otherwise its bindings never resolve
            var interact = InputManager.Playable.Interact;
            InputSystem.Update();
            Press(_keyboard.fKey);
            InputSystem.Update();
            Assert.IsTrue(interact.GetKey, "Fの押下がInputSystemへ届いていない");
        }
    }
}
