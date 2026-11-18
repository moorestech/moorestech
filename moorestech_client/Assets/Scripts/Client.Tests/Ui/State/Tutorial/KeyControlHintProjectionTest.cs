using System;
using System.Text.RegularExpressions;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.Tutorial;
using Client.Ui.State.Tutorial.KeyControlHint;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Tutorial
{
    /// <summary>
    /// キー操作ヒントが今の画面と blocking スキットで絞られることを確かめる（L0）
    /// Verifies key control hints are filtered by the current screen and blocking skit (L0)
    /// </summary>
    public class KeyControlHintProjectionTest
    {
        [Test]
        public void 語彙外のuiStateは一度だけ警告しどの画面とも一致させない()
        {
            var data = TutorialOverlayProjectionTest.Data(1, new TutorialKeyControlElementData { ElementId = "k1", TutorialGuid = Guid.NewGuid().ToString(), KeyName = "Tab", UiState = "GameScreeen" });
            var projection = new KeyControlHintProjection();

            LogAssert.Expect(LogType.Warning, new Regex("Unknown keyControl uiState: GameScreeen"));
            Assert.AreEqual(0, projection.Project(data, UIStateEnum.GameScreen, false).Hints.Count);
            Assert.AreEqual(0, projection.Project(data, UIStateEnum.GameScreen, false).Hints.Count);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
