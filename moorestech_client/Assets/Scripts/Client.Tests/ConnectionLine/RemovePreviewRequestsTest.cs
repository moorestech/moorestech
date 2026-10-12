using Client.Game.InGame.UI.UIState.State.RemovePreview;
using Client.Game.InGame.UI.UIState.State;
using NUnit.Framework;

namespace Client.Tests.ConnectionLine
{
    /// <summary>
    ///     赤プレビューは要求者が残っている間は外れないことを検証する（巻き込み表示が他者の赤を消さない）
    ///     Verifies the red preview stays while any requester remains (a cascade reset never clears someone else's red)
    /// </summary>
    public class RemovePreviewRequestsTest
    {
        [Test]
        public void RedStaysUntilTheLastRequesterReleases()
        {
            var requests = new RemovePreviewRequests();
            var ownHover = new object();
            var cascadingPole = new object();

            // 最初の要求だけが「赤を付ける」合図になる
            // Only the first request signals "apply red"
            Assert.IsTrue(requests.Add(ownHover));
            Assert.IsFalse(requests.Add(cascadingPole));

            // 電柱側の解除では戻らず最後の解除で戻る
            // Not reset by the pole's release, only by the last requester's
            Assert.IsFalse(requests.Remove(cascadingPole));
            Assert.IsTrue(requests.Remove(ownHover));
        }

        [Test]
        public void DuplicateAndUnknownRequestsAreIgnored()
        {
            // 二重要求・未登録の解除は状態不変
            // A duplicate request or unknown release changes nothing
            var requests = new RemovePreviewRequests();
            var requester = new object();
            Assert.IsTrue(requests.Add(requester));
            Assert.IsFalse(requests.Add(requester));
            Assert.IsFalse(requests.Remove(new object()));
            Assert.IsTrue(requests.Remove(requester));
            Assert.IsFalse(requests.Remove(requester));
        }
    }
}
