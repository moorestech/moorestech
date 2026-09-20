using System;
using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.ProgressBar;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Localization;
using Client.Tests.Common;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Interact
{
    /// <summary>
    ///     採掘対象が主対象ならFは近傍へ回らず別キーだけ回る（ADR 0065）
    ///     While a mining target is primary, F is never forwarded but other keys still are (ADR 0065)
    /// </summary>
    public class MiningPrimaryHoldKeyForwardTest : InputTestFixture
    {
        private static readonly Guid ToolItemGuid = new("00000000-0000-0000-1234-000000000001");

        private ToolMismatchMiningTarget _miningTarget;
        private GameObject _candidateObject;
        private InteractController _controller;
        private ScriptedInteractTargetSelector _selector;

        public override void Setup()
        {
            base.Setup();
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            InputSystem.AddDevice<Keyboard>();
            TestReflection.ResetInputManagerCache();
            Localize.Initialize();

            // 掘れない採掘対象でもFは回さない
            // Even a target that cannot be mined keeps F from being forwarded
            _miningTarget = new ToolMismatchMiningTarget();
            _candidateObject = new GameObject("NearbyTapCandidate");
            _selector = new ScriptedInteractTargetSelector();
            _selector.SetNext(_miningTarget);
            _controller = new InteractController(CreateEquipment(), _selector, new ProgressBarState(), new MouseCursorTooltipState());
            InputSystem.Update();
        }

        public override void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_candidateObject);
            UnityEngine.Object.DestroyImmediate(_miningTarget.GameObject);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void 採掘対象が主対象ならFは近傍の単押し候補へ回らない()
        {
            var openAction = new StubTapInteractAction(InputManager.Playable.Interact, LocalizationKeys.Ui.Tooltip.InteractOpenBlock, UIStateEnum.SubInventory);
            _selector.AddCandidate(new StubTapInteractable(_candidateObject, openAction));

            InputManager.Playable.Interact.SetKeyDownForTest(true);
            var result = _controller.ManualUpdate();
            InputManager.Playable.Interact.SetKeyDownForTest(false);

            Assert.IsFalse(result.IsHandled);
            Assert.AreEqual(0, openAction.ExecutedCount);
            Assert.AreEqual(0, _miningTarget.SendAttackCount);
        }

        [Test]
        public void 採掘対象が主対象でも別キーは近傍の単押し候補へ回る()
        {
            var rideAction = new StubTapInteractAction(InputManager.Playable.Ride, LocalizationKeys.Ui.Tooltip.InteractRideTrain, UIStateEnum.TrainHUDScreen);
            _selector.AddCandidate(new StubTapInteractable(_candidateObject, rideAction));

            InputManager.Playable.Ride.SetKeyDownForTest(true);
            var result = _controller.ManualUpdate();
            InputManager.Playable.Ride.SetKeyDownForTest(false);

            Assert.AreEqual(UIStateEnum.TrainHUDScreen, result.TransitContext.NextStateEnum);
            Assert.AreEqual(1, rideAction.ExecutedCount);
        }

        private static LocalPlayerEquipment CreateEquipment()
        {
            var toolItemId = MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
            var equipment = new LocalPlayerEquipment();
            equipment.Initialize(new List<IItemStack> { ServerContext.ItemStackFactory.Create(toolItemId, 1) }, 0);
            return equipment;
        }
    }
}
