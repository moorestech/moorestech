using System;
using System.Linq;
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

            store.AddOutlineHighlight("recipe.craft-button", Guid.NewGuid());

            var session = store.GetCurrent().Sessions.Single();
            var element = (TutorialOutlineElementData)session.Elements.Single();
            Assert.AreEqual(challengeId.ToString(), session.ChallengeId);
            Assert.AreEqual("recipe.craft-button", element.AnchorId);
            Assert.AreEqual(TutorialOutlineElementData.KindName, element.Kind);
        }

        // challenge完了時は同じsessionの要素をsessionごと畳む
        // Completing the challenge drops the whole session along with its elements
        [Test]
        public void EndSessionDropsTheChallengeSession()
        {
            var store = new TutorialPresentationStateStore();
            var challengeId = Guid.NewGuid();
            store.BeginSession(challengeId);
            store.AddOutlineHighlight("recipe.craft-button", Guid.NewGuid());

            store.EndSession(challengeId);

            Assert.IsEmpty(store.GetCurrent().Sessions);
        }

        // 別challengeの完了通知は他challengeのsessionを消さない
        // Completing one challenge does not clear another challenge's session
        [Test]
        public void OtherChallengeCompletionDoesNotClearRemainingSession()
        {
            var store = new TutorialPresentationStateStore();
            var challengeId = Guid.NewGuid();
            store.BeginSession(challengeId);
            store.AddOutlineHighlight("recipe.craft-button", Guid.NewGuid());
            var current = store.GetCurrent();

            store.EndSession(Guid.NewGuid());

            Assert.AreSame(current, store.GetCurrent());
        }

        // 同時currentの2challengeは互いの提示を残したまま並存する
        // Two simultaneously current challenges coexist without erasing each other's presentation
        [Test]
        public void BeginSessionKeepsThePreviousChallengePresentation()
        {
            var store = new TutorialPresentationStateStore();
            var firstChallengeId = Guid.NewGuid();
            store.BeginSession(firstChallengeId);
            store.AddOutlineHighlight("recipe.craft-button", Guid.NewGuid());
            var secondChallengeId = Guid.NewGuid();

            store.BeginSession(secondChallengeId);
            store.AddDragGuide("build-menu.entry-block-934c0ef9", "hotbar.hud");

            var sessions = store.GetCurrent().Sessions;
            Assert.AreEqual(2, sessions.Length);
            Assert.AreEqual(firstChallengeId.ToString(), sessions[0].ChallengeId);
            Assert.AreEqual("recipe.craft-button", ((TutorialOutlineElementData)sessions[0].Elements.Single()).AnchorId);
            Assert.AreEqual(secondChallengeId.ToString(), sessions[1].ChallengeId);
            Assert.AreEqual("hotbar.hud", ((TutorialDragGuideElementData)sessions[1].Elements.Single()).ToAnchorId);
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
            var guide = (TutorialDragGuideElementData)current.Sessions.Single().Elements.Single();
            Assert.AreEqual("build-menu.entry-block-934c0ef9", guide.FromAnchorId);
            Assert.AreEqual("hotbar.hud", guide.ToAnchorId);
            Assert.AreEqual(TutorialDragGuideElementData.KindName, guide.Kind);
            Assert.AreEqual(revisionBeforeAdd + 1, current.Revision);
        }

        // RemoveElementは対象の要素だけを消し同一sessionの他要素は残す
        // RemoveElement clears only the targeted element and leaves the session's other elements intact
        [Test]
        public void RemoveElementClearsOnlyTargetElement()
        {
            var store = new TutorialPresentationStateStore();
            store.BeginSession(Guid.NewGuid());
            store.AddOutlineHighlight("recipe.craft-button", Guid.NewGuid());
            var guideView = store.AddDragGuide("build-menu.entry-block-934c0ef9", "hotbar.hud");
            var sessionId = store.GetCurrent().Sessions.Single().TutorialSessionId;

            guideView.CompleteTutorial();

            var session = store.GetCurrent().Sessions.Single();
            Assert.AreEqual(sessionId, session.TutorialSessionId);
            Assert.AreEqual(1, session.Elements.Length);
            Assert.IsInstanceOf<TutorialOutlineElementData>(session.Elements.Single());
        }

        // セッション不一致のRemoveElementは無視され現在状態は変化しない
        // RemoveElement from a stale session id is ignored and leaves the current state untouched
        [Test]
        public void RemoveElementIgnoresMismatchedSessionId()
        {
            var store = new TutorialPresentationStateStore();
            store.BeginSession(Guid.NewGuid());
            store.AddDragGuide("build-menu.entry-block-934c0ef9", "hotbar.hud");
            var current = store.GetCurrent();

            store.RemoveElement("stale-session-id", current.Sessions.Single().Elements.Single().ElementId);

            Assert.AreSame(current, store.GetCurrent());
        }

        // 存在しないelementIdのRemoveElementはrevisionを据え置く
        // RemoveElement with an unknown element id leaves the revision unchanged
        [Test]
        public void RemoveElementWithUnknownElementIdKeepsRevision()
        {
            var store = new TutorialPresentationStateStore();
            store.BeginSession(Guid.NewGuid());
            store.AddDragGuide("build-menu.entry-block-934c0ef9", "hotbar.hud");
            var current = store.GetCurrent();

            store.RemoveElement(current.Sessions.Single().TutorialSessionId, "unknown-element-id");

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
            store.AddOutlineHighlight("recipe.craft-button", Guid.NewGuid());
            store.AddDragGuide("build-menu.entry-block-934c0ef9", "hotbar.hud");

            store.EndSession(challengeId);

            Assert.IsEmpty(store.GetCurrent().Sessions.SelectMany(session => session.Elements));
        }

        // 枠線は常にtutorialGuidを載せる(ラベル有無はWeb側のt()解決結果で決まる)
        // An outline always carries the tutorialGuid; the web side decides label presence from the t() result
        [Test]
        public void AddOutlineHighlightCarriesLabelTutorialGuid()
        {
            var store = new TutorialPresentationStateStore();
            store.BeginSession(Guid.NewGuid());

            store.AddOutlineHighlight("recipe.craft-button", new Guid("11111111-1111-4111-8111-111111111111"));
            store.AddOutlineHighlight("hotbar.hud", new Guid("22222222-2222-4222-8222-222222222222"));

            var elements = store.GetCurrent().Sessions.Single().Elements.Cast<TutorialOutlineElementData>().ToArray();
            Assert.AreEqual("11111111-1111-4111-8111-111111111111", elements[0].LabelTutorialGuid);
            Assert.AreEqual("22222222-2222-4222-8222-222222222222", elements[1].LabelTutorialGuid);
        }
        // keyControlは独立kind
        // keyControl is its own kind
        [Test]
        public void AddKeyControlHintPublishesKeyControlKind()
        {
            var store = new TutorialPresentationStateStore();
            var challengeId = Guid.NewGuid();
            store.BeginSession(challengeId);

            var view = store.AddKeyControlHint("22222222-2222-4222-8222-222222222222", "Tab", "GameScreen");

            var element = (TutorialKeyControlElementData)store.GetCurrent().Sessions.Single().Elements.Single();
            Assert.AreEqual(TutorialKeyControlElementData.KindName, element.Kind);
            Assert.AreEqual("22222222-2222-4222-8222-222222222222", element.TutorialGuid);
            Assert.AreEqual("Tab", element.KeyName);
            Assert.AreEqual("GameScreen", element.UiState);

            view.CompleteTutorial();
            Assert.IsEmpty(store.GetCurrent().Sessions.Single().Elements);
        }
    }
}
