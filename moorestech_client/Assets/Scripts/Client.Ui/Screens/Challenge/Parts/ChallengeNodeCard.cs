using System;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Challenge.Tree;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Challenge.Parts
{
    /// <summary>
    /// チャレンジのノードカード 1 枚。web: moorestech_web/webui/src/features/challenge/ChallengeNodeCard.tsx
    /// One challenge node card; web: moorestech_web/webui/src/features/challenge/ChallengeNodeCard.tsx
    /// </summary>
    /// <remarks>
    /// PaperSurface は sealed なので、根の PaperSurface を組み立てて Root で渡す。根は拾う（Ignore だと押下の対象がビューポートに変わる。C4-d）。指のカーソルは付けない（Web のカードに cursor: pointer もタップも無く、ビューポートの grab を継ぐ）
    /// PaperSurface is sealed, so this builds the root PaperSurface and hands it out as Root; the root picks (Ignore would retarget presses to the viewport; C4-d); no pointer cursor (the Web card has neither cursor: pointer nor taps and inherits the viewport grab)
    /// </remarks>
    public sealed class ChallengeNodeCard
    {
        private readonly LocalizedLabel _state;

        public PaperSurface Root { get; }

        public ChallengeNodeCard(Guid challengeGuid)
        {
            // 根は Paper の面。アンカーは GUID
            // Root is the Paper face; anchor is the GUID
            var key = challengeGuid.ToString();
            Root = new PaperSurface($"challenge-node-{key}");
            Root.AddToClassList("challenge-list__card");
            TutorialAnchor.Mark(Root, AnchorIds.ChallengeNode(key));

            // 名前・概要・状態を縦に積む
            // Stack title, summary and state
            var title = AddText(UiTypographyClass.TextMd, "challenge-list__card-title");
            title.AddToClassList(UiTypographyClass.TextBold);
            title.SetText(UiText.FromContent(ContentLocalizationKeys.ChallengeTitle(challengeGuid)));
            var summary = AddText(UiTypographyClass.TextSm, "challenge-list__card-summary");
            summary.SetText(UiText.FromContent(ContentLocalizationKeys.ChallengeSummary(challengeGuid)));
            _state = AddText(UiTypographyClass.TextXs, "challenge-list__card-state");

            // 輪は Web の outline。拾わない子を最後に重ねる
            // Ring is the Web outline; a non-picking child goes on top last
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("challenge-list__card-ring");
            Root.Add(ring);

            #region Internal

            // 文字は拾わない。押下はカードの根へ浮かぶ
            // Text never picks; presses bubble to the card root
            LocalizedLabel AddText(string sizeClass, string partClass)
            {
                var label = new LocalizedLabel();
                label.AddToClassList(sizeClass);
                label.AddToClassList(partClass);
                UiPickingIgnore.ApplyToSubtree(label);
                Root.Add(label);
                return label;
            }

            #endregion
        }

        public void Render(ChallengeNodeCardViewModel node)
        {
            // 状態は修飾クラスと文言キーで決まる
            // State is a modifier class and label key
            Root.EnableInClassList("challenge-list__card--locked", node.State == ChallengeNodeState.Locked);
            Root.EnableInClassList("challenge-list__card--current", node.State == ChallengeNodeState.Current);
            Root.EnableInClassList("challenge-list__card--completed", node.State == ChallengeNodeState.Completed);
            _state.SetText(new UiText(node.StateLabelKey));
            Root.style.scale = new Scale(new Vector2(node.Scale.x, node.Scale.y));
        }
    }
}
