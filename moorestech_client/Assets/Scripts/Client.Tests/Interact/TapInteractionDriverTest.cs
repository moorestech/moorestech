using System;
using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Interact.Tap;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Localization;
using Client.Tests.Common;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Interact
{
    /// <summary>
    ///     単押しアクションのヒント提示とキー実行を検証
    ///     Verifies tap hint presentation and key execution
    /// </summary>
    public class TapInteractionDriverTest : InputTestFixture
    {
        private MouseCursorTooltipState _tooltip;
        private GameObject _targetObject;
        private GameObject _candidateObject;

        public override void Setup()
        {
            base.Setup();
            TestReflection.ResetInputManagerCache();

            // 文言解決は実辞書を通す
            // Resolve text through the real dictionary
            Localize.Initialize();

            _tooltip = new MouseCursorTooltipState();

            _targetObject = new GameObject("TapTarget");
            _candidateObject = new GameObject("CandidateTapTarget");
        }

        public override void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_candidateObject);
            UnityEngine.Object.DestroyImmediate(_targetObject);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void アクションのヒントが行として出てキー押下で遷移が返る()
        {
            InputSystem.AddDevice<Keyboard>();
            var target = new StubTapInteractable(
                _targetObject,
                new StubAction(InputManager.Playable.Interact, LocalizationKeys.Ui.Tooltip.InteractOpenTrainInventory, UIStateEnum.SubInventory),
                new StubAction(InputManager.Playable.Ride, LocalizationKeys.Ui.Tooltip.InteractRideTrain, UIStateEnum.TrainHUDScreen));
            var driver = new TapInteractionDriver(_tooltip);
            var selector = new ScriptedInteractTargetSelector();
            selector.SetNext(target);
            InputSystem.Update();

            Assert.IsFalse(driver.Step(target, selector.Scan()).IsHandled);
            var lines = _tooltip.GetPresentation().Lines;
            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.InteractOpenTrainInventory.Key, lines[0].Key.Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.InteractRideTrain.Key, lines[1].Key.Key);

            // 押下キーのみ実行しヒントは畳む
            // Only the pressed key's action runs and the hints fold away
            InputManager.Playable.Ride.SetKeyDownForTest(true);
            var result = driver.Step(target, selector.Scan());
            InputManager.Playable.Ride.SetKeyDownForTest(false);
            Assert.IsTrue(result.IsHandled);
            Assert.AreEqual(UIStateEnum.TrainHUDScreen, result.TransitContext.NextStateEnum);
            Assert.IsFalse(_tooltip.GetPresentation().Visible);
        }

        [Test]
        public void 押されていないキーのアクションだけならヒントを出して遷移しない()
        {
            InputSystem.AddDevice<Keyboard>();
            var target = new StubTapInteractable(
                _targetObject,
                new StubAction(InputManager.Playable.Ride, LocalizationKeys.Ui.Tooltip.InteractRideTrain, UIStateEnum.TrainHUDScreen));
            var driver = new TapInteractionDriver(_tooltip);
            var selector = new ScriptedInteractTargetSelector();
            selector.SetNext(target);
            InputSystem.Update();

            Assert.IsFalse(driver.Step(target, selector.Scan()).IsHandled);
            Assert.IsTrue(_tooltip.GetPresentation().Visible);

            // 対象から離れたらヒントも消える
            // Leaving the target clears the hint as well
            driver.Clear();
            Assert.IsFalse(_tooltip.GetPresentation().Visible);
        }

        [Test]
        public void 主対象が応じないキーは応じる別候補へ回る()
        {
            InputSystem.AddDevice<Keyboard>();
            var primaryAction = new StubAction(InputManager.Playable.Interact, LocalizationKeys.Ui.Tooltip.InteractOpenBlock, UIStateEnum.SubInventory);
            var candidateAction = new StubAction(InputManager.Playable.Ride, LocalizationKeys.Ui.Tooltip.InteractRideTrain, UIStateEnum.TrainHUDScreen);
            var target = new StubTapInteractable(_targetObject, primaryAction);
            var driver = new TapInteractionDriver(_tooltip);
            var selector = new ScriptedInteractTargetSelector();
            selector.SetNext(target);
            selector.AddCandidate(new StubTapInteractable(_candidateObject, candidateAction));
            InputSystem.Update();

            InputManager.Playable.Ride.SetKeyDownForTest(true);
            var result = driver.Step(target, selector.Scan());
            InputManager.Playable.Ride.SetKeyDownForTest(false);

            Assert.AreEqual(UIStateEnum.TrainHUDScreen, result.TransitContext.NextStateEnum);
            Assert.AreEqual(1, candidateAction.ExecutedCount);
            Assert.AreEqual(0, primaryAction.ExecutedCount);
        }

        [Test]
        public void 主対象が応じるキーは転送されず主対象が実行する()
        {
            InputSystem.AddDevice<Keyboard>();
            var primaryAction = new StubAction(InputManager.Playable.Interact, LocalizationKeys.Ui.Tooltip.InteractOpenBlock, UIStateEnum.SubInventory);
            var candidateAction = new StubAction(InputManager.Playable.Interact, LocalizationKeys.Ui.Tooltip.InteractOpenTrainInventory, UIStateEnum.TrainHUDScreen);
            var target = new StubTapInteractable(_targetObject, primaryAction);
            var driver = new TapInteractionDriver(_tooltip);
            var selector = new ScriptedInteractTargetSelector();
            selector.SetNext(target);
            selector.AddCandidate(new StubTapInteractable(_candidateObject, candidateAction));
            InputSystem.Update();

            InputManager.Playable.Interact.SetKeyDownForTest(true);
            var result = driver.Step(target, selector.Scan());
            InputManager.Playable.Interact.SetKeyDownForTest(false);

            Assert.AreEqual(UIStateEnum.SubInventory, result.TransitContext.NextStateEnum);
            Assert.AreEqual(1, primaryAction.ExecutedCount);
            Assert.AreEqual(0, candidateAction.ExecutedCount);
        }

        private sealed class StubTapInteractable : ITapInteractable
        {
            public GameObject GameObject { get; }
            public bool IsInteractAvailable => true;
            public IReadOnlyList<ITapInteractAction> Actions { get; }

            public StubTapInteractable(GameObject gameObject, params ITapInteractAction[] actions)
            {
                GameObject = gameObject;
                Actions = actions;
            }

            public void SetHighlighted(bool highlighted)
            {
            }
        }

        private sealed class StubAction : ITapInteractAction
        {
            private readonly UIStateEnum _nextState;

            public InputKey Key { get; }
            public LocalizationKey HintKey { get; }
            public IReadOnlyList<string> HintParams => Array.Empty<string>();
            public int ExecutedCount { get; private set; }

            public StubAction(InputKey key, LocalizationKey hintKey, UIStateEnum nextState)
            {
                Key = key;
                HintKey = hintKey;
                _nextState = nextState;
            }

            public InteractExecuteResult Execute()
            {
                ExecutedCount++;
                return InteractExecuteResult.Transit(new UITransitContext(_nextState));
            }
        }
    }
}
