using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Game.Topics.BuildMenu;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi
{
    /// <summary>
    /// 所持変化の再配信ゲート（ビルドメニュー表示中のみ）の回帰試験
    /// Regression test for the inventory-change republish gate (only while the build menu is up)
    /// </summary>
    public class BuildMenuTopicRepublishTest
    {
        [Test]
        public void 所持変化の再配信はビルドメニュー表示中だけ行う()
        {
            var controlObject = new GameObject("BuildMenuTopicRepublishTest.Control");
            var control = controlObject.AddComponent<UIStateControl>();

            try
            {
                SetCurrentState(control, UIStateEnum.GameScreen);
                var gate = new BuildMenuInventoryRepublishGate(control);
                Assert.IsFalse(gate.ShouldRepublish());

                SetCurrentState(control, UIStateEnum.BuildMenu);
                Assert.IsTrue(gate.ShouldRepublish());
            }
            finally
            {
                Object.DestroyImmediate(controlObject);
            }

            #region Internal

            void SetCurrentState(UIStateControl target, UIStateEnum state)
            {
                typeof(UIStateControl).GetProperty(nameof(UIStateControl.CurrentState)).SetValue(target, state);
            }

            #endregion
        }
    }
}
