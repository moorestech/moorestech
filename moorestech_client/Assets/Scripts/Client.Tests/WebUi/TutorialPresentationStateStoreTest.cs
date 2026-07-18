using System;
using Client.Game.InGame.Tutorial;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    public class TutorialPresentationStateStoreTest
    {
        // challenge開始時のsessionへDOM highlightを宣言する
        // Declare a DOM highlight in the session opened for the challenge
        [Test]
        public void AddHighlightPublishesAnchorAndKind()
        {
            var store = new TutorialPresentationStateStore();
            var challengeId = Guid.NewGuid();
            store.BeginSession(challengeId);

            store.AddHighlight("recipe.craft-button", "spotlight", "Hold to craft");

            var current = store.GetCurrent();
            Assert.AreEqual(challengeId.ToString(), current.ChallengeId);
            Assert.AreEqual("recipe.craft-button", current.Highlights[0].AnchorId);
            Assert.AreEqual("spotlight", current.Highlights[0].Kind);
        }

        // challenge完了時は同じsessionのhighlightを全て消す
        // Clear every highlight in the same session when the challenge completes
        [Test]
        public void EndSessionClearsHighlights()
        {
            var store = new TutorialPresentationStateStore();
            var challengeId = Guid.NewGuid();
            store.BeginSession(challengeId);
            var sessionId = store.GetCurrent().TutorialSessionId;
            store.AddHighlight("recipe.craft-button", "outline", "Craft");

            store.EndSession(challengeId);

            Assert.AreEqual(sessionId, store.GetCurrent().TutorialSessionId);
            Assert.IsEmpty(store.GetCurrent().Highlights);
        }

        // 過去challengeの完了通知は現在sessionを消さない
        // Completion of an older challenge does not clear the current session
        [Test]
        public void OlderChallengeCompletionDoesNotClearCurrentSession()
        {
            var store = new TutorialPresentationStateStore();
            store.BeginSession(Guid.NewGuid());
            store.AddHighlight("recipe.craft-button", "outline", "Craft");
            var current = store.GetCurrent();

            store.EndSession(Guid.NewGuid());

            Assert.AreSame(current, store.GetCurrent());
        }
    }
}
