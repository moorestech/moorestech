using System;
using Client.Game.InGame.Tutorial;
using Client.Ui.State.Tutorial.Overlay;
using Client.Localization;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Tutorial
{
    /// <summary>
    /// チュートリアルの演出データから枠と DragGuide の ViewModel を作ることを確かめる（L0）
    /// Verifies the overlay view model is projected from tutorial presentation data (L0)
    /// </summary>
    public class TutorialOverlayProjectionTest
    {
        public static readonly Guid LabelGuid = new("11111111-2222-3333-4444-555555555555");

        public static TutorialPresentationData Data(int revision, params TutorialOverlayElementData[] elements)
        {
            return new TutorialPresentationData { Revision = revision, Sessions = new[] { new TutorialSessionData { TutorialSessionId = "s1", ChallengeId = "c1", Elements = elements } } };
        }

        public static TutorialOutlineElementData Outline(string elementId, string anchorId, string labelGuid)
        {
            return new TutorialOutlineElementData { ElementId = elementId, AnchorId = anchorId, PaddingPx = 8, LabelTutorialGuid = labelGuid };
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">keyControlが混在してもoutline描画件数は変わらない</web-test>
        [Test]
        public void keyControlが混ざっても枠の件数は変わらない()
        {
            var keyControl = new TutorialKeyControlElementData { ElementId = "k1", TutorialGuid = LabelGuid.ToString(), KeyName = "Tab", UiState = "GameScreen" };
            var model = new TutorialOverlayProjection().Project(Data(3, Outline("o1", "pause.save", null), keyControl));

            Assert.AreEqual(1, model.Outlines.Count);
            Assert.AreEqual(0, model.DragGuides.Count);
            Assert.AreEqual("s1:o1", model.Outlines[0].Key);
        }

        [Test]
        public void ラベルのGUIDがある枠だけがcontentの文を持つ()
        {
            // EditMode では辞書が未初期化のことがあるので、解決の前に初期化する
            // The dictionary may be uninitialized in EditMode, so initialize it before resolving
            Localize.Initialize();
            var model = new TutorialOverlayProjection().Project(Data(1, Outline("o1", "pause.save", LabelGuid.ToString()), Outline("o2", "pause.back", "")));

            var labeled = (TutorialOutlineCue.Labeled)model.Outlines[0];
            Assert.AreEqual(UiTextSource.Content, labeled.Label.Source);
            Assert.AreEqual(UiText.FromContent(ContentLocalizationKeys.ChallengeTutorialText(LabelGuid)).Resolve(), labeled.Label.Resolve());
            Assert.IsInstanceOf<TutorialOutlineCue.Plain>(model.Outlines[1]);
        }

        [Test]
        public void 読めないラベルのGUIDは理由をLogErrorに出しラベル無しで枠を出す()
        {
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("tutorialGuid is not a GUID: 'not-a-guid'"));
            var model = new TutorialOverlayProjection().Project(Data(1, Outline("o1", "pause.save", "not-a-guid")));
            Assert.AreEqual(1, model.Outlines.Count);
            Assert.IsInstanceOf<TutorialOutlineCue.Plain>(model.Outlines[0]);
        }

        [Test]
        public void 読めないラベルのGUIDはデータが変わって組み直してもラベル無しの枠のまま()
        {
            // 1 回だけ LogError する仕組みは TutorialTextFactoryTest が持つ。ここは 2 回目も Plain で出ることだけ見る
            // TutorialTextFactoryTest covers the log-once rule; here we only check the rebuild still yields Plain
            var projection = new TutorialOverlayProjection();
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("tutorialGuid is not a GUID: 'not-a-guid'"));
            projection.Project(Data(1, Outline("o1", "pause.save", "not-a-guid")));
            var rebuilt = projection.Project(Data(2, Outline("o1", "pause.save", "not-a-guid"), Outline("o2", "pause.back", null)));
            Assert.AreEqual(2, rebuilt.Outlines.Count);
            Assert.IsInstanceOf<TutorialOutlineCue.Plain>(rebuilt.Outlines[0]);
        }
    }
}
