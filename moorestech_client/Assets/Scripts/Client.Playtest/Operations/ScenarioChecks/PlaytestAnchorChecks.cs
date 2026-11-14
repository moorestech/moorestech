using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Tutorial;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Root;
using Client.Ui.Screens.Tutorial.Anchor;
using Client.Ui.State.Tutorial.Logic;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Playtest.Operations
{
    // マスタのチュートリアルが指すアンカー 1 件
    // One anchor a master tutorial points at
    public readonly struct PlaytestMasterAnchor
    {
        public readonly string AnchorId;
        public readonly Guid TutorialGuid;
        public readonly Guid ChallengeGuid;
        public readonly string ChallengeTitle;

        public PlaytestMasterAnchor(string anchorId, Guid tutorialGuid, Guid challengeGuid, string challengeTitle)
        {
            AnchorId = anchorId;
            TutorialGuid = tutorialGuid;
            ChallengeGuid = challengeGuid;
            ChallengeTitle = challengeTitle;
        }
    }

    // マスタの uiHighLight を集め、実ゲームの木でアンカーと枠を照合する
    // Collects master uiHighLight entries and checks the anchor and frame in the live tree
    public static class PlaytestAnchorChecks
    {
        private const string OutlineName = "tutorial-outline";
        private const string OutlineRingClass = "tutorial-outline__ring";
        private const float CenterTolerancePx = 5f;

        public static IReadOnlyList<PlaytestMasterAnchor> CollectUiHighlightAnchors()
        {
            var anchors = new List<PlaytestMasterAnchor>();
            foreach (var category in MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements)
            foreach (var challenge in category.Challenges)
            foreach (var tutorial in challenge.Tutorials)
            {
                if (tutorial.TutorialType != TutorialsElement.TutorialTypeConst.uiHighLight) continue;
                var param = (UiHighLightTutorialParam)tutorial.TutorialParam;
                anchors.Add(new PlaytestMasterAnchor(param.HighLightAnchorId, tutorial.TutorialGuid, challenge.ChallengeGuid, challenge.Title));
            }
            return anchors;
        }

        public static async UniTask CheckOutlineOnAnchor(PlaytestDriver p, PlaytestMasterAnchor anchor)
        {
            // 今の画面の木でアンカーが 1 つに解決する
            // The anchor resolves to exactly one element in the current tree
            var root = GameUiRoot.Instance.GetRootElement();
            var resolution = TutorialAnchorRegistry.Resolve(root, anchor.AnchorId);
            var unique = resolution as TutorialAnchorResolution.Unique;
            p.Assert(unique != null, $"{anchor.ChallengeTitle}: {anchor.AnchorId} が 1 つに解決する（{resolution.GetType().Name}）");
            if (unique == null) return;

            // 本番と同じ入口で枠を出す。本番のチャレンジが既に枠を出しうるので、足す前の枠を控えて増えた 1 件だけを自分の枠とする
            // Show the frame via the production entry; production may already show frames, so snapshot first and take only the added one
            var before = new HashSet<VisualElement>(root.Query(OutlineName).ToList());
            var view = TutorialPresentationStateStore.Instance.AddOutlineHighlight(anchor.AnchorId, anchor.TutorialGuid);
            VisualElement own = null;
            await p.Until(() => (own = root.Query(OutlineName).ToList().FirstOrDefault(e => !before.Contains(e))) != null, 5f, $"{anchor.AnchorId} の枠が 1 つ増える");
            await UniTask.DelayFrame(2);
            var ring = own.Q(className: OutlineRingClass);

            // 見える範囲は本番の枠と同じ判定（TutorialAncestorClipWalker）で根の座標に取る。呼び手が視口へ寄せてから呼ぶので、収まらなければ落とす
            // Take the visible region with the production walker in root space; callers bring the anchor into view first, so failing to fit is an error
            var anchorBound = root.WorldToLocal(unique.Element.worldBound);
            var visible = TutorialAncestorClipWalker.ClipOf(unique.Element, root);
            var fullyVisible = visible.Expand(0.5f).ContainsRect(new TutorialClipRect(anchorBound.xMin, anchorBound.yMin, anchorBound.xMax, anchorBound.yMax));
            p.Assert(fullyVisible, $"{anchor.AnchorId} が見える範囲に収まる（{anchorBound} / 見える範囲 {visible.Left:F0},{visible.Top:F0}-{visible.Right:F0},{visible.Bottom:F0}）");
            var distance = Vector2.Distance(root.WorldToLocal(ring.worldBound).center, anchorBound.center);
            p.Assert(distance <= CenterTolerancePx, $"{anchor.AnchorId} の枠の中心がアンカーの中心から {distance:F1}px（{CenterTolerancePx}px 以内）");
            await p.Screenshot($"anchor-{anchor.AnchorId}");

            // 完了で自分の枠だけが木から外れ、控えた本番の枠は残る
            // Completing detaches only our frame; the snapshotted production frames stay
            view.CompleteTutorial();
            await p.Until(() => own.panel == null, 5f, $"{anchor.AnchorId} の自分の枠が消える");
            p.Assert(before.All(e => e.panel != null), $"{anchor.AnchorId} の完了で本番の枠を消していない");
        }
    }
}
