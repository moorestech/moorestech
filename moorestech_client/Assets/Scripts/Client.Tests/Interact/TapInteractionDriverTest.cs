using System;
using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Localization;
using Client.Tests.Common;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using TMPro;
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
        private GameObject _tooltipObject;
        private GameObject _targetObject;

        public override void Setup()
        {
            base.Setup();
            TestReflection.ResetInputManagerCache();

            // 文言解決は実辞書を通す
            // Resolve text through the real dictionary
            Localize.Initialize();

            _tooltipObject = new GameObject("MouseCursorTooltip");
            _tooltipObject.SetActive(false);
            var tooltip = _tooltipObject.AddComponent<MouseCursorTooltip>();
            TestReflection.SetField(tooltip, "canvasGroup", _tooltipObject.AddComponent<CanvasGroup>());
            TestReflection.SetField(tooltip, "itemName", _tooltipObject.AddComponent<TextMeshProUGUI>());
            _tooltipObject.SetActive(true);
            TestReflection.InvokePrivate(tooltip, "Awake");

            _targetObject = new GameObject("TapTarget");
        }

        public override void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_targetObject);
            UnityEngine.Object.DestroyImmediate(_tooltipObject);
            TestReflection.SetStaticProperty(typeof(MouseCursorTooltip), "Instance", null);
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
            var driver = new TapInteractionDriver();
            InputSystem.Update();

            Assert.IsNull(driver.Step(target));
            var lines = MouseCursorTooltip.Instance.GetPresentation().Lines;
            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.InteractOpenTrainInventory.Key, lines[0].Key.Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.InteractRideTrain.Key, lines[1].Key.Key);

            // 押下キーのみ実行しヒントは畳む
            // Only the pressed key's action runs and the hints fold away
            InputManager.Playable.Ride.SetKeyDownForTest(true);
            var transit = driver.Step(target);
            InputManager.Playable.Ride.SetKeyDownForTest(false);
            Assert.AreEqual(UIStateEnum.TrainHUDScreen, transit.NextStateEnum);
            Assert.IsFalse(MouseCursorTooltip.Instance.GetPresentation().Visible);
        }

        [Test]
        public void 押されていないキーのアクションだけならヒントを出して遷移しない()
        {
            InputSystem.AddDevice<Keyboard>();
            var target = new StubTapInteractable(
                _targetObject,
                new StubAction(InputManager.Playable.Ride, LocalizationKeys.Ui.Tooltip.InteractRideTrain, UIStateEnum.TrainHUDScreen));
            var driver = new TapInteractionDriver();
            InputSystem.Update();

            Assert.IsNull(driver.Step(target));
            Assert.IsTrue(MouseCursorTooltip.Instance.GetPresentation().Visible);

            // 対象から離れたらヒントも消える
            // Leaving the target clears the hint as well
            driver.Clear();
            Assert.IsFalse(MouseCursorTooltip.Instance.GetPresentation().Visible);
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

            public StubAction(InputKey key, LocalizationKey hintKey, UIStateEnum nextState)
            {
                Key = key;
                HintKey = hintKey;
                _nextState = nextState;
            }

            public UITransitContext Execute()
            {
                return new UITransitContext(_nextState);
            }
        }
    }
}
