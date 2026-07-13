using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.ViewMode
{
    public class UIStateControlViewModeTest : InputTestFixture
    {
        private GameObject _gameObject;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _gameObject = new GameObject("UIStateControl");
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            AimPointProvider.SetMode(PlayerViewMode.ThirdPerson);
            Object.DestroyImmediate(_gameObject);
            base.TearDown();
        }

        [Test]
        public void TrainTransitionDisablesFpsAndGameScreenReturnRestoresIt()
        {
            var gameState = new StubUIState();
            var trainState = new StubUIState();
            var (control, controller, applier) = CreateControl((UIStateEnum.GameScreen, gameState), (UIStateEnum.TrainHUDScreen, trainState));
            control.Initialize(UIStateEnum.GameScreen, new UITransitContext(UIStateEnum.GameScreen));
            controller.ToggleViewMode();

            gameState.SetNextState(UIStateEnum.TrainHUDScreen);
            InvokeUnityMessage(control, "Update");
            Assert.AreEqual(false, applier.LastFirstPersonCamera);

            trainState.SetNextState(UIStateEnum.GameScreen);
            InvokeUnityMessage(control, "Update");
            Assert.AreEqual(true, applier.LastFirstPersonCamera);
        }

        [Test]
        public void BuildMenuUpdateHandlesViewToggle()
        {
            var buildMenuState = new StubUIState();
            var (control, controller, _) = CreateControl((UIStateEnum.BuildMenu, buildMenuState));
            control.Initialize(UIStateEnum.BuildMenu, new UITransitContext(UIStateEnum.BuildMenu));

            Press(_keyboard.vKey);
            InvokeUnityMessage(control, "Update");

            Assert.AreEqual(PlayerViewMode.FirstPerson, controller.CurrentMode);
        }

        [Test]
        public void ViewInputIsAppliedBeforeConcreteStateUpdate()
        {
            var gameState = new StubUIState();
            var (control, controller, _) = CreateControl((UIStateEnum.GameScreen, gameState));
            control.Initialize(UIStateEnum.GameScreen, new UITransitContext(UIStateEnum.GameScreen));
            gameState.SetUpdateObservation(() => controller.CurrentMode);

            Press(_keyboard.vKey);
            InvokeUnityMessage(control, "Update");

            Assert.AreEqual(PlayerViewMode.FirstPerson, gameState.ObservedMode);
        }

        [Test]
        public void FocusRestoreDoesNotOverrideTrainCameraPolicy()
        {
            var trainState = new FocusRestoringStubUIState();
            var (control, _, applier) = CreateControl((UIStateEnum.TrainHUDScreen, trainState));
            control.Initialize(UIStateEnum.TrainHUDScreen, new UITransitContext(UIStateEnum.TrainHUDScreen));
            applier.Calls.Clear();

            InvokeUnityMessage(control, "OnApplicationFocus", true);

            Assert.IsEmpty(applier.Calls);
            Assert.AreEqual(1, trainState.RestoreCount);
        }

        private (UIStateControl, PlayerViewModeController, FakePlayerViewApplier) CreateControl(params (UIStateEnum, IUIState)[] states)
        {
            var dictionary = CreateDictionary();
            var stateDictionary = GetStateDictionary(dictionary);
            foreach (var (state, implementation) in states) stateDictionary[state] = implementation;

            var applier = new FakePlayerViewApplier();
            var controller = new PlayerViewModeController(applier);
            var control = _gameObject.AddComponent<UIStateControl>();
            control.Construct(dictionary, controller);
            return (control, controller, applier);
        }

        private static UIStateDictionary CreateDictionary()
        {
            return new UIStateDictionary(null, null, null, null, null, null, null, null, null, null, null, null);
        }

        private static Dictionary<UIStateEnum, IUIState> GetStateDictionary(UIStateDictionary dictionary)
        {
            var field = typeof(UIStateDictionary).GetField("_stateDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
            return (Dictionary<UIStateEnum, IUIState>)field.GetValue(dictionary);
        }

        private static void InvokeUnityMessage(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, arguments);
        }

        private class StubUIState : IUIState
        {
            private UITransitContext _nextContext;
            private System.Func<PlayerViewMode> _updateObservation;
            public PlayerViewMode ObservedMode { get; private set; }

            public void SetNextState(UIStateEnum state)
            {
                _nextContext = new UITransitContext(state);
            }

            public void SetUpdateObservation(System.Func<PlayerViewMode> updateObservation)
            {
                _updateObservation = updateObservation;
            }

            public void OnEnter(UITransitContext context)
            {
            }

            public UITransitContext GetNextUpdate()
            {
                if (_updateObservation != null) ObservedMode = _updateObservation();
                var nextContext = _nextContext;
                _nextContext = null;
                return nextContext;
            }

            public void OnExit()
            {
            }
        }

        private class FocusRestoringStubUIState : StubUIState, IApplicationFocusRestorer
        {
            public int RestoreCount { get; private set; }

            public void RestoreAfterApplicationFocus()
            {
                RestoreCount++;
            }
        }
    }
}
