using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.Modal;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    // BP名入力→入力モーダルの保留→応答→状態への書き戻しが、Webホスト無しでも DI の中だけで回ることを確かめる
    // Verifies blueprint-name input -> pending input modal -> reply -> write-back runs inside DI alone, without the web host
    internal static class ModalStateProbe
    {
        public static async UniTask AssertAvailableAsync(IObjectResolver resolver)
        {
            var modal = resolver.Resolve<ModalRequestService>();
            var nameInput = resolver.Resolve<BlueprintNameInputState>();
            string confirmedName = null;
            using var confirmSubscription = nameInput.OnConfirm.Subscribe(name => confirmedName = name);

            nameInput.Open();
            Assert.IsNotNull(modal.Pending, "BP名入力を開いても入力モーダルが保留されない");
            Assert.IsTrue(modal.Pending.RequiresInput);

            Assert.IsTrue(modal.Respond(modal.Pending.Id, "confirm", "hostless-bp"));
            for (var i = 0; i < 30 && confirmedName == null; i++) await UniTask.Yield();
            Assert.AreEqual("hostless-bp", confirmedName, "モーダルの確定がBP名入力へ書き戻されない");
            Assert.IsNull(modal.Pending);
        }
    }
}
