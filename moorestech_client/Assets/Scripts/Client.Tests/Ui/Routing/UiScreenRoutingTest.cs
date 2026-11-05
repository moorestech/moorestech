using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using Client.Ui.State.Routing;
using NUnit.Framework;

namespace Client.Tests.Ui.Routing
{
    public class UiScreenRoutingTest
    {
        // 入れ子サブステートを持たない state の期待画面。全 UIStateEnum を網羅する
        // Expected screen for each state without a nested sub-state; covers every UIStateEnum
        private static readonly Dictionary<UIStateEnum, UiScreen> ExpectedWithoutSubState = new()
        {
            { UIStateEnum.GameScreen, UiScreen.None },
            { UIStateEnum.PlayerInventory, UiScreen.PlayerInventory },
            { UIStateEnum.SubInventory, UiScreen.SubInventory },
            { UIStateEnum.PauseMenu, UiScreen.PauseMenu },
            { UIStateEnum.DeleteBar, UiScreen.None },
            { UIStateEnum.Story, UiScreen.None },
            { UIStateEnum.PlaceBlock, UiScreen.None },
            { UIStateEnum.ChallengeList, UiScreen.ChallengeList },
            { UIStateEnum.ResearchTree, UiScreen.ResearchTree },
            { UIStateEnum.Debug, UiScreen.None },
            { UIStateEnum.TrainHUDScreen, UiScreen.TrainHud },
            { UIStateEnum.BuildMenu, UiScreen.BuildMenu },
        };

        [Test]
        public void 全UIStateが期待表どおりの画面に解決される()
        {
            foreach (UIStateEnum state in Enum.GetValues(typeof(UIStateEnum)))
            {
                Assert.IsTrue(ExpectedWithoutSubState.ContainsKey(state), $"期待表に {state} が無い");
                Assert.AreEqual(ExpectedWithoutSubState[state], UiScreenRouting.Resolve(state, null), state.ToString());
            }
        }

        [Test]
        public void PlayerInventoryはインベントリ画面()
        {
            Assert.AreEqual(UiScreen.PlayerInventory, UiScreenRouting.Resolve(UIStateEnum.PlayerInventory, null));
        }

        [Test]
        public void SubInventoryはブロック画面()
        {
            Assert.AreEqual(UiScreen.SubInventory, UiScreenRouting.Resolve(UIStateEnum.SubInventory, null));
        }

        [Test]
        public void BuildMenuはbuildMenu画面に解決される()
        {
            Assert.AreEqual(UiScreen.BuildMenu, UiScreenRouting.Resolve(UIStateEnum.BuildMenu, null));
        }

        [Test]
        public void ChallengeListはchallengeList画面に解決される()
        {
            Assert.AreEqual(UiScreen.ChallengeList, UiScreenRouting.Resolve(UIStateEnum.ChallengeList, null));
        }

        [Test]
        public void PauseMenuはpauseMenu画面に解決される()
        {
            Assert.AreEqual(UiScreen.PauseMenu, UiScreenRouting.Resolve(UIStateEnum.PauseMenu, null));
        }

        [Test]
        public void TrainHUDScreenは入れ子stateでHUDとPauseを分ける()
        {
            Assert.AreEqual(UiScreen.TrainHud, UiScreenRouting.Resolve(UIStateEnum.TrainHUDScreen, NestedPauseSubStateEnum.GameScreen));
            Assert.AreEqual(UiScreen.TrainPause, UiScreenRouting.Resolve(UIStateEnum.TrainHUDScreen, NestedPauseSubStateEnum.PauseMenuScreen));
        }

        [Test]
        public void Storyは入れ子のPauseMenuScreenだけをskitPause画面にする()
        {
            Assert.AreEqual(UiScreen.None, UiScreenRouting.Resolve(UIStateEnum.Story, NestedPauseSubStateEnum.GameScreen));
            Assert.AreEqual(UiScreen.SkitPause, UiScreenRouting.Resolve(UIStateEnum.Story, NestedPauseSubStateEnum.PauseMenuScreen));
            Assert.AreEqual(UiScreen.None, UiScreenRouting.Resolve(UIStateEnum.Story, null));
        }

        [Test]
        public void PlaceBlockは画面を占有しないHUD_stateとして扱う()
        {
            Assert.AreEqual(UiScreen.None, UiScreenRouting.Resolve(UIStateEnum.PlaceBlock, null));
        }

        [Test]
        public void GameScreenはパネル無し()
        {
            Assert.AreEqual(UiScreen.None, UiScreenRouting.Resolve(UIStateEnum.GameScreen, null));
        }

        // Web の SCREEN_TRAITS の写し。列順: grab, pauseMenu, skitInput, backdrop, alwaysOnHud, trainHud
        // Copy of the web SCREEN_TRAITS; column order: grab, pauseMenu, skitInput, backdrop, alwaysOnHud, trainHud
        private static readonly Dictionary<UiScreen, bool[]> ExpectedTraits = new()
        {
            { UiScreen.None, new[] { false, false, true, false, true, false } },
            { UiScreen.PlayerInventory, new[] { true, false, true, true, true, false } },
            { UiScreen.SubInventory, new[] { true, false, true, true, true, false } },
            { UiScreen.ResearchTree, new[] { true, false, true, true, false, false } },
            { UiScreen.BuildMenu, new[] { false, false, true, true, true, false } },
            { UiScreen.ChallengeList, new[] { false, false, true, true, true, false } },
            { UiScreen.PauseMenu, new[] { false, true, true, true, true, false } },
            { UiScreen.TrainHud, new[] { false, false, true, false, true, true } },
            { UiScreen.TrainPause, new[] { false, true, true, true, true, true } },
            { UiScreen.SkitPause, new[] { false, true, false, true, true, false } },
        };

        [Test]
        public void 全画面の性質がWebの表と一致する()
        {
            foreach (UiScreen screen in Enum.GetValues(typeof(UiScreen)))
            {
                Assert.IsTrue(ExpectedTraits.ContainsKey(screen), $"期待表に {screen} が無い");
                var traits = UiScreenTraitsTable.Get(screen);
                var actual = new[] { traits.Grab, traits.PauseMenu, traits.SkitInput, traits.Backdrop, traits.AlwaysOnHud, traits.TrainHud };
                CollectionAssert.AreEqual(ExpectedTraits[screen], actual, screen.ToString());
            }
        }

        [Test]
        public void 常時表示族を引っ込めるのは研究画面のみ()
        {
            foreach (UiScreen screen in Enum.GetValues(typeof(UiScreen)))
            {
                Assert.AreEqual(screen != UiScreen.ResearchTree, UiScreenTraitsTable.Get(screen).AlwaysOnHud, screen.ToString());
            }
        }

        [Test]
        public void スキット入力を閉じるのはskitPauseのみ()
        {
            foreach (UiScreen screen in Enum.GetValues(typeof(UiScreen)))
            {
                Assert.AreEqual(screen != UiScreen.SkitPause, UiScreenTraitsTable.Get(screen).SkitInput, screen.ToString());
            }
        }
    }
}
