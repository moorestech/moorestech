using System;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.UIState.NestedPause
{
    /// <summary>
    ///     入れ子ポーズの出入りで移動可否が再適用されることを確認
    ///     Verifies the movement lock is re-applied when a nested pause opens and closes
    /// </summary>
    public class UIStateNestedPauseMovementLockTest
    {
        private GameObject _controlObject;

        [SetUp]
        public void SetUp()
        {
            _controlObject = new GameObject("UIStateNestedPauseMovementLock");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_controlObject);
        }

        [Test]
        public void 入れ子ポーズの開閉で移動可否が追従する()
        {
            var screen = new StubNestedPauseScreenState();
            var player = new FakePlayerObjectController();
            var control = _controlObject.AddComponent<UIStateControl>();
            control.Construct(CreateDictionary(screen, new StubWorldState()), player);

            control.Initialize(UIStateEnum.GameScreen, new UITransitContext(UIStateEnum.GameScreen));
            Assert.AreEqual(false, player.LastUiLock, "サブステートの宣言が初期適用されていない");

            screen.PushSubState(NestedPauseSubStateEnum.PauseMenuScreen, true);
            Assert.AreEqual(true, player.LastUiLock, "入れ子ポーズを開いても移動が止まらない");

            screen.PushSubState(NestedPauseSubStateEnum.GameScreen, false);
            Assert.AreEqual(false, player.LastUiLock, "入れ子ポーズを閉じても移動が止まったまま");
        }

        [Test]
        public void 画面を出た後のサブステート遷移は移動可否へ届かない()
        {
            var screen = new StubNestedPauseScreenState();
            var world = new StubWorldState();
            var player = new FakePlayerObjectController();
            var control = _controlObject.AddComponent<UIStateControl>();
            control.Construct(CreateDictionary(screen, world), player);
            control.Initialize(UIStateEnum.GameScreen, new UITransitContext(UIStateEnum.GameScreen));

            // GameScreen（入れ子持ち）からBuildMenuへ抜ける
            // Leave the nested-pause screen for the BuildMenu
            InvokeUpdate(control);
            var callCountAfterExit = player.UiLockCallCount;

            screen.PushSubState(NestedPauseSubStateEnum.PauseMenuScreen, true);

            Assert.AreEqual(callCountAfterExit, player.UiLockCallCount, "退出済み画面のサブステート遷移が移動可否を書き換えた");
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
            typeof(UIStateControl).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        private class StubNestedPauseScreenState : IUIState, INestedPauseScreenState
        {
            private readonly Subject<NestedPauseSubStateEnum> _onSubStateChanged = new();
            private bool _locksPlayerMovement;

            public NestedPauseSubStateEnum SubState { get; private set; }
            public IObservable<Unit> OnPresentationChanged => Observable.Never<Unit>();
            public IObservable<NestedPauseSubStateEnum> OnSubStateChanged => _onSubStateChanged;

            // 本番の入れ子ステートマシンと同じく、サブステートを変えてから通知する
            // Mirror the production nested state machine: change the sub-state, then notify
            public void PushSubState(NestedPauseSubStateEnum subState, bool locksPlayerMovement)
            {
                SubState = subState;
                _locksPlayerMovement = locksPlayerMovement;
                _onSubStateChanged.OnNext(subState);
            }

            public void OnEnter(UITransitContext context)
            {
            }

            public UITransitContext GetNextUpdate()
            {
                return new UITransitContext(UIStateEnum.BuildMenu);
            }

            public void OnExit()
            {
            }

            public bool LocksPlayerMovement()
            {
                return _locksPlayerMovement;
            }

            public IReadOnlyList<KeyHint> GetKeyHints()
            {
                return Array.Empty<KeyHint>();
            }

            public bool RequestClosePauseMenu()
            {
                return false;
            }
        }

        private class StubWorldState : IUIState
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

            public bool LocksPlayerMovement()
            {
                return false;
            }

            public IReadOnlyList<KeyHint> GetKeyHints()
            {
                return Array.Empty<KeyHint>();
            }
        }
    }
}
