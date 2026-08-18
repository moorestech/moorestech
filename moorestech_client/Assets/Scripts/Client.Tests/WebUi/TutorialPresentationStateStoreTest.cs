using System;
using Client.Game.InGame.Tutorial;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    public class TutorialPresentationStateStoreTest
    {
        [Test]
        public void AddOutlineHighlightPublishesAnchorAndOutlineKind()
        {
            var store = new TutorialPresentationStateStore();
            var challengeId = Guid.NewGuid();
            store.BeginSession(challengeId);

            store.AddOutlineHighlight("recipe.craft-button");

            var current = store.GetCurrent();
            Assert.AreEqual(challengeId.ToString(), current.ChallengeId);
            Assert.AreEqual("recipe.craft-button", current.Highlights[0].AnchorId);
            Assert.AreEqual("outline", current.Highlights[0].Kind);
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
            store.AddOutlineHighlight("recipe.craft-button");

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
            store.AddOutlineHighlight("recipe.craft-button");
            var current = store.GetCurrent();

            store.EndSession(Guid.NewGuid());

            Assert.AreSame(current, store.GetCurrent());
        }

        // AddDragGuideがfrom/toanchorとrevision加算を伴って公開されること
        // AddDragGuide publishes the from/to anchors and increments the revision
        [Test]
        public void AddDragGuidePublishesFromAndToAnchor()
        {
            var store = new TutorialPresentationStateStore();
            store.BeginSession(Guid.NewGuid());
            var revisionBeforeAdd = store.GetCurrent().Revision;

            store.AddDragGuide("build-menu.entry-block-934c0ef9", "hotbar.hud");

            var current = store.GetCurrent();
            Assert.AreEqual("build-menu.entry-block-934c0ef9", current.DragGuides[0].FromAnchorId);
            Assert.AreEqual("hotbar.hud", current.DragGuides[0].ToAnchorId);
            Assert.AreEqual(revisionBeforeAdd + 1, current.Revision);
        }

        // RemoveDragGuideは対象のguideだけを消しハイライトは残す
        // RemoveDragGuide clears only the targeted guide, leaving highlights intact
        [Test]
        public void RemoveDragGuideClearsOnlyTargetGuideAndKeepsHighlights()
        {
            var store = new TutorialPresentationStateStore();
            store.BeginSession(Guid.NewGuid());
            store.AddOutlineHighlight("recipe.craft-button");
            var guideView = store.AddDragGuide("build-menu.entry-block-934c0ef9", "hotbar.hud");
            var sessionId = store.GetCurrent().TutorialSessionId;

            guideView.CompleteTutorial();

            var current = store.GetCurrent();
            Assert.IsEmpty(current.DragGuides);
            Assert.AreEqual(1, current.Highlights.Length);
            Assert.AreEqual(sessionId, current.TutorialSessionId);
        }

        // RemoveHighlightは対象のhighlightだけを消しガイドは残す
        // RemoveHighlight clears only the targeted highlight, leaving drag guides intact
        [Test]
        public void RemoveHighlightClearsOnlyTargetHighlightAndKeepsDragGuides()
        {
            var store = new TutorialPresentationStateStore();
            store.BeginSession(Guid.NewGuid());
            var highlightView = store.AddOutlineHighlight("recipe.craft-button");
            store.AddDragGuide("build-menu.entry-block-934c0ef9", "hotbar.hud");

            highlightView.CompleteTutorial();

            var current = store.GetCurrent();
            Assert.IsEmpty(current.Highlights);
            Assert.AreEqual(1, current.DragGuides.Length);
        }

        // セッション不一致のRemoveDragGuideは無視され現在状態は変化しない
        // RemoveDragGuide from a stale session id is ignored and leaves the current state untouched
        [Test]
        public void RemoveDragGuideIgnoresMismatchedSessionId()
        {
            var store = new TutorialPresentationStateStore();
            store.BeginSession(Guid.NewGuid());
            store.AddDragGuide("build-menu.entry-block-934c0ef9", "hotbar.hud");
            var current = store.GetCurrent();

            store.RemoveDragGuide("stale-session-id", current.DragGuides[0].GuideId);

            Assert.AreSame(current, store.GetCurrent());
        }

        // 存在しないguideIdのRemoveDragGuideはrevisionを据え置く
        // RemoveDragGuide with an unknown guide id leaves the revision unchanged
        [Test]
        public void RemoveDragGuideWithUnknownGuideIdKeepsRevision()
        {
            var store = new TutorialPresentationStateStore();
            store.BeginSession(Guid.NewGuid());
            store.AddDragGuide("build-menu.entry-block-934c0ef9", "hotbar.hud");
            var current = store.GetCurrent();

            store.RemoveDragGuide(current.TutorialSessionId, "unknown-guide-id");

            Assert.AreSame(current, store.GetCurrent());
        }

        // challenge完了時はhighlightとdragGuideの両方を消す
        // Completing the challenge clears both highlights and drag guides
        [Test]
        public void EndSessionClearsDragGuidesAlongsideHighlights()
        {
            var store = new TutorialPresentationStateStore();
            var challengeId = Guid.NewGuid();
            store.BeginSession(challengeId);
            store.AddOutlineHighlight("recipe.craft-button");
            store.AddDragGuide("build-menu.entry-block-934c0ef9", "hotbar.hud");

            store.EndSession(challengeId);

            var current = store.GetCurrent();
            Assert.IsEmpty(current.Highlights);
            Assert.IsEmpty(current.DragGuides);
        }
    }
}
