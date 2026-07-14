using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UIState
{
    public class UIStateControlTest
    {
        private GameObject _controlObject;

        [SetUp]
        public void SetUp()
        {
            _controlObject = new GameObject("UIStateControl");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_controlObject);
        }

        [Test]
        public void TransitionDoesNotRequirePlayerViewController()
        {
            var firstState = new StubUIState(UIStateEnum.BuildMenu);
            var secondState = new StubUIState(null);
            var dictionary = CreateDictionary(firstState, secondState);
            var control = _controlObject.AddComponent<UIStateControl>();
            control.Construct(dictionary);
            control.Initialize(UIStateEnum.GameScreen, new UITransitContext(UIStateEnum.GameScreen));

            InvokeUpdate(control);

            Assert.AreEqual(UIStateEnum.BuildMenu, control.CurrentState);
            Assert.AreEqual(1, firstState.ExitCount);
            Assert.AreEqual(1, secondState.EnterCount);

            #region Internal

            UIStateDictionary CreateDictionary(IUIState first, IUIState second)
            {
                var result = new UIStateDictionary(null, null, null, null, null, null, null, null, null, null, null, null);
                var field = typeof(UIStateDictionary).GetField("_stateDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
                var states = (Dictionary<UIStateEnum, IUIState>)field.GetValue(result);
                states[UIStateEnum.GameScreen] = first;
                states[UIStateEnum.BuildMenu] = second;
                return result;
            }

            void InvokeUpdate(UIStateControl target)
            {
                var method = typeof(UIStateControl).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                method.Invoke(target, null);
            }

            #endregion
        }

        private class StubUIState : IUIState
        {
            private readonly UIStateEnum? _nextState;
            public int EnterCount { get; private set; }
            public int ExitCount { get; private set; }

            public StubUIState(UIStateEnum? nextState)
            {
                _nextState = nextState;
            }

            public void OnEnter(UITransitContext context)
            {
                EnterCount++;
            }

            public UITransitContext GetNextUpdate()
            {
                return _nextState.HasValue ? new UITransitContext(_nextState.Value) : null;
            }

            public void OnExit()
            {
                ExitCount++;
            }
        }
    }
}
