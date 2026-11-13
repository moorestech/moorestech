using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Ui.State.Challenge.Tree.Selection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using Source = Client.Tests.Ui.State.Challenge.ChallengeProgressStateTestSource;

namespace Client.Tests.Ui.State.Challenge
{
    /// <summary>
    /// カテゴリ選択は一覧中だけ保つ（L0）
    /// Category selection holds only inside the list (L0)
    /// </summary>
    public class ChallengeCategorySelectionStoreTest
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void 一覧の中では選んだカテゴリを保ち離れると無しに戻る()
        {
            _host = new GameObject("ChallengeCategorySelectionStoreTest");
            var routing = UiRoutingStateTestFactory.CreateSwitchable(_host, UIStateEnum.GameScreen, new[] { UIStateEnum.GameScreen, UIStateEnum.ChallengeList }, out var control);
            var store = new ChallengeCategorySelectionStore(routing);
            store.Initialize();
            Assert.AreSame(ChallengeCategorySelection.None, store.Current.Value, "初期");

            // 一覧へ入って選ぶと保つ
            // Entering the list and selecting keeps it
            UiRoutingStateTestFactory.Transit(control, UIStateEnum.ChallengeList);
            store.Select(Source.Category2);
            Assert.IsTrue(store.Current.Value.Is(Source.Category2));

            // 離れると無しに戻る
            // Leaving resets it to none
            UiRoutingStateTestFactory.Transit(control, UIStateEnum.GameScreen);
            Assert.AreSame(ChallengeCategorySelection.None, store.Current.Value, "離れた後");
        }
    }
}
