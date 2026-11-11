using Client.Skit.Skit;
using Client.Skit.UI;
using Client.Ui.State.Skit;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Skit
{
    /// <summary>
    /// モデルが購読の時点の提示から始まり、store の変化を追うことを確かめる（L0）
    /// Verifies that the model starts from the snapshot at subscription and follows store changes (L0)
    /// </summary>
    public class SkitPresentationModelTest
    {
        [Test]
        public void 購読の時点の提示から始まりstoreの変化を追う()
        {
            var store = new SkitPresentationStateStore();
            store.BeginBlocking(new SkitActionContext());
            store.PresentBlockingText("Moore", "Blocking message");
            var model = new SkitPresentationModel(store);
            model.Initialize();
            Assert.AreEqual("Blocking message", model.Current.Value.Body);

            store.End();
            Assert.AreEqual(SkitSurface.None, model.Current.Value.Surface);
        }
    }
}
