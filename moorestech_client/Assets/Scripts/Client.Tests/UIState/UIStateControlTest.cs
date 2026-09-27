using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Player;
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
            var firstState = new StubUIState(UIStateEnum.BuildMenu, false);
            var secondState = new StubUIState(null, false);
            var dictionary = CreateDictionary(firstState, secondState);
            var control = _controlObject.AddComponent<UIStateControl>();
            control.Construct(dictionary, new RecordingPlayerObjectController());
            control.Initialize(UIStateEnum.GameScreen, new UITransitContext(UIStateEnum.GameScreen));

            InvokeUpdate(control);

            Assert.AreEqual(UIStateEnum.BuildMenu, control.CurrentState);
            Assert.AreEqual(1, firstState.ExitCount);
            Assert.AreEqual(1, secondState.EnterCount);
        }

        [Test]
        public void 画面の宣言どおりに自機の移動を止めて戻す()
        {
            var worldState = new StubUIState(UIStateEnum.BuildMenu, false);
            var menuState = new StubUIState(UIStateEnum.GameScreen, true);
            var player = new RecordingPlayerObjectController();
            var control = _controlObject.AddComponent<UIStateControl>();
            control.Construct(CreateDictionary(worldState, menuState), player);

            // 初期画面にも宣言を適用する
            // The initial screen's declaration is applied as well
            control.Initialize(UIStateEnum.GameScreen, new UITransitContext(UIStateEnum.GameScreen));
            Assert.AreEqual(false, player.LastUiLock);

            InvokeUpdate(control);
            Assert.AreEqual(UIStateEnum.BuildMenu, control.CurrentState);
            Assert.AreEqual(true, player.LastUiLock, "メニュー画面へ入っても移動が止まらない");

            InvokeUpdate(control);
            Assert.AreEqual(UIStateEnum.GameScreen, control.CurrentState);
            Assert.AreEqual(false, player.LastUiLock, "メニューを閉じても移動が止まったまま");
        }

        private static UIStateDictionary CreateDictionary(IUIState gameScreen, IUIState buildMenu)
        {
            var result = new UIStateDictionary(null, null, null, null, null, null, null, null, null, null, null, null);
            var field = typeof(UIStateDictionary).GetField("_stateDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
            var states = (Dictionary<UIStateEnum, IUIState>)field.GetValue(result);
            states[UIStateEnum.GameScreen] = gameScreen;
            states[UIStateEnum.BuildMenu] = buildMenu;
            return result;
        }

        private static void InvokeUpdate(UIStateControl target)
        {
            var method = typeof(UIStateControl).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, null);
        }

        private class StubUIState : IUIState
        {
            private readonly UIStateEnum? _nextState;
            private readonly bool _locksPlayerMovement;
            public int EnterCount { get; private set; }
            public int ExitCount { get; private set; }

            public StubUIState(UIStateEnum? nextState, bool locksPlayerMovement)
            {
                _nextState = nextState;
                _locksPlayerMovement = locksPlayerMovement;
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

            public bool LocksPlayerMovement()
            {
                return _locksPlayerMovement;
            }

            public IReadOnlyList<KeyHint> GetKeyHints()
            {
                return System.Array.Empty<KeyHint>();
            }
        }

        private class RecordingPlayerObjectController : IPlayerObjectController
        {
            public bool? LastUiLock { get; private set; }
            public Vector3 Position => Vector3.zero;

            public void SetMovementLockedByUi(bool isLocked)
            {
                LastUiLock = isLocked;
            }

            public void SetPlayerPosition(Vector3 playerPos)
            {
            }

            public void SetActive(bool active)
            {
            }

            public void SetAnimationState(string state)
            {
            }

            public void SetMovementLockedByDebug(bool isLocked)
            {
            }

            public void SetModelVisible(bool visible)
            {
            }
        }
    }
}
