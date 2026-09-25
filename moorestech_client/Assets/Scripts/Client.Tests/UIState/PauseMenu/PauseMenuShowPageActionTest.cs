using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Game.Topics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UIState.PauseMenu
{
    public class PauseMenuShowPageActionTest
    {
        [TestCase("settings", PauseMenuPage.Settings)]
        [TestCase("bugReport", PauseMenuPage.BugReport)]
        [TestCase("top", PauseMenuPage.Top)]
        public void 契約文字列の画面へ移る(string page, PauseMenuPage expected)
        {
            var service = new PauseMenuStateService();
            var handler = new PauseMenuShowPageActionHandler(service);

            var result = handler.ExecuteAsync(new JObject { ["page"] = page }).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok, result.Error);
            Assert.AreEqual(expected, service.CurrentPage.Value);
        }

        [Test]
        public void 不正な画面名は理由をログに出して拒否する()
        {
            var service = new PauseMenuStateService();
            service.ShowPage(PauseMenuPage.Settings);
            var handler = new PauseMenuShowPageActionHandler(service);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("ポーズメニューの画面名が不正"));
            var result = handler.ExecuteAsync(new JObject { ["page"] = "Settings" }).GetAwaiter().GetResult();

            Assert.IsFalse(result.Ok);
            Assert.AreEqual("invalid_page", result.Error);
            Assert.AreEqual(PauseMenuPage.Settings, service.CurrentPage.Value);
        }

        [Test]
        public void 契約文字列は往復で同じ画面に戻る()
        {
            foreach (PauseMenuPage page in System.Enum.GetValues(typeof(PauseMenuPage)))
            {
                Assert.IsTrue(PauseMenuPageContract.TryParse(PauseMenuPageContract.ToContractText(page), out var parsed));
                Assert.AreEqual(page, parsed);
            }
        }
    }
}
