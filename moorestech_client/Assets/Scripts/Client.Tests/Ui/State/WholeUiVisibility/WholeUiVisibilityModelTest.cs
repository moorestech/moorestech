using Client.Ui.State.WholeUiVisibility;
using NUnit.Framework;

namespace Client.Tests.Ui.State.WholeUiVisibility
{
    /// <summary>
    /// 全体を出すかが「Ctrl+U で隠していない かつ カットシーンでない」になることを確かめる（L0。Web App.tsx の !uiVisible || cutScene）
    /// Verifies that the whole UI shows exactly when it is not hidden by Ctrl+U and no cutscene plays (L0; Web App.tsx !uiVisible || cutScene)
    /// </summary>
    public class WholeUiVisibilityModelTest
    {
        private FakeWholeUiVisibilitySource _source;
        private WholeUiVisibilityModel _model;

        [SetUp]
        public void CreateModel()
        {
            _source = new FakeWholeUiVisibilitySource();
            _model = new WholeUiVisibilityModel(_source);
            _model.Initialize();
        }

        [Test]
        public void 起動直後は全体を出す()
        {
            Assert.IsTrue(_model.IsShown.Value);
        }

        [Test]
        public void CtrlUで隠すと全体が隠れ戻すと出る()
        {
            _source.SetUiRootVisible(false);
            Assert.IsFalse(_model.IsShown.Value);
            _source.SetUiRootVisible(true);
            Assert.IsTrue(_model.IsShown.Value);
        }

        /// <web-test file="e2e/tests/phaseC4.spec.ts">cutscene state withdraws every web UI layer</web-test>
        [Test]
        public void カットシーンの間は全体が隠れ抜けると出る()
        {
            _source.SetCutScenePlaying(true);
            Assert.IsFalse(_model.IsShown.Value);
            _source.SetUiRootVisible(false);
            _source.SetCutScenePlaying(false);
            Assert.IsFalse(_model.IsShown.Value, "カットシーンを抜けても Ctrl+U で隠している間は出さない");
            _source.SetUiRootVisible(true);
            Assert.IsTrue(_model.IsShown.Value);
        }
    }
}
