using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Tests.ViewMode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Client.Tests.UIState
{
    public class UIStateControlTextInputFocusTest : InputTestFixture
    {
        private GameObject _controlObject;
        private GameObject _eventSystemObject;
        private GameObject _inputObject;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _controlObject = new GameObject("UIStateControl");
            _eventSystemObject = new GameObject("EventSystem");
            _eventSystemObject.AddComponent<EventSystem>();
            _inputObject = new GameObject("InputField");
        }

        public override void TearDown()
        {
            AimPointProvider.SetMode(AimPointMode.Mouse);
            Object.DestroyImmediate(_inputObject);
            Object.DestroyImmediate(_eventSystemObject);
            Object.DestroyImmediate(_controlObject);
            base.TearDown();
        }

        [Test]
        public void FocusedTextInputSuppressesViewToggleThroughUIStateControl()
        {
            var inputFieldType = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("TMPro.TMP_InputField"))
                .First(type => type != null);
            var inputField = _inputObject.AddComponent(inputFieldType);
            EventSystem.current.SetSelectedGameObject(_inputObject);
            SetField(inputField, "m_AllowInput", true);

            var state = new StubUIState();
            var dictionary = new UIStateDictionary(null, null, null, null, null, null, null, null, null, null, null, null);
            var stateDictionary = GetStateDictionary(dictionary);
            stateDictionary[UIStateEnum.GameScreen] = state;

            var controller = new PlayerViewModeController(new FakePlayerViewApplier());
            var control = _controlObject.AddComponent<UIStateControl>();
            control.Construct(dictionary, controller);
            control.Initialize(UIStateEnum.GameScreen, new UITransitContext(UIStateEnum.GameScreen));

            Press(_keyboard.vKey);
            InvokePrivate(control, "Update");

            Assert.AreEqual(PlayerViewMode.ThirdPerson, controller.CurrentMode);

            #region Internal

            static Dictionary<UIStateEnum, IUIState> GetStateDictionary(UIStateDictionary dictionary)
            {
                var field = typeof(UIStateDictionary).GetField("_stateDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
                return (Dictionary<UIStateEnum, IUIState>)field.GetValue(dictionary);
            }

            static void InvokePrivate(object target, string methodName)
            {
                var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                method.Invoke(target, null);
            }

            static void SetField(object target, string fieldName, object value)
            {
                var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                field.SetValue(target, value);
            }

            #endregion
        }

        private class StubUIState : IUIState
        {
            public void OnEnter(UITransitContext context)
            {
            }

            public UITransitContext GetNextUpdate()
            {
                return null;
            }

            public void OnExit()
            {
            }
        }
    }
}
