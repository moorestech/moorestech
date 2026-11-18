using System;
using System.Collections.Generic;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Skit.UI;
using Client.Tests.Common;
using Client.Tests.UIState.Fakes;
using Client.Ui.State.SkitPresence;
using Client.Ui.State.Tutorial.KeyControlHint;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Tests.Ui.State.Tutorial
{
    /// <summary>
    /// 注入した store と、ログイン復元で決まる初期画面からキー操作ヒントが組まれることを確かめる（L0）
    /// Verifies key control hints are built from the injected store and the initial screen set by login restore (L0)
    /// </summary>
    public class KeyControlHintModelTest
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
        public void 列車HUDで復元するとその画面のヒントが初期化直後に出る()
        {
            // 列車HUD用のヒントだけを持つ store を注入する
            // Inject a store holding only a train HUD hint
            var store = new TutorialPresentationStateStore();
            store.AddKeyControlHint(Guid.NewGuid().ToString(), "W", UIStateEnum.TrainHUDScreen.ToString());
            var control = _controlObject.AddComponent<UIStateControl>();
            control.Construct(CreateDictionary(), new FakePlayerObjectController());
            var model = new KeyControlHintModel(control, new BlockingSkitPresence(new SkitPresentationStateStore()), store);
            model.Initialize();
            Assert.AreEqual(0, model.Current.Value.Hints.Count);

            // 復元で列車HUDに入った直後に、遷移を待たずヒントが組み直される
            // Right after restore enters the train HUD, hints are rebuilt without waiting for a transition
            control.Initialize(UIStateEnum.TrainHUDScreen, new UITransitContext(UIStateEnum.TrainHUDScreen));
            Assert.AreEqual(1, model.Current.Value.Hints.Count);
            Assert.AreEqual("W", model.Current.Value.Hints[0].KeyName);
        }

        private static UIStateDictionary CreateDictionary()
        {
            var result = new UIStateDictionary(null, null, null, null, null, null, null, null, null, null, null, null);
            var states = TestReflection.GetField<Dictionary<UIStateEnum, IUIState>>(result, "_stateDictionary");
            states[UIStateEnum.GameScreen] = new IdleUIState();
            states[UIStateEnum.TrainHUDScreen] = new IdleUIState();
            return result;
        }

        private class IdleUIState : IUIState
        {
            public void OnEnter(UITransitContext context) { }
            public UITransitContext GetNextUpdate() => null;
            public void OnExit() { }
            public bool LocksPlayerMovement() => false;
            public bool AllowsSprintToggle() => true;
            public IReadOnlyList<KeyHint> GetKeyHints() => Array.Empty<KeyHint>();
        }
    }
}
