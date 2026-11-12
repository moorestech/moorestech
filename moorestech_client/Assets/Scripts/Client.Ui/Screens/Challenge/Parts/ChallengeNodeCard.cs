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
    /// チャレンジのノードカード1枚（Paper の面に名前・概要・状態）。web: moorestech_web/webui/src/features/challenge/ChallengeNodeCard.tsx
    /// One challenge node card (title, summary and state on a Paper face); web: moorestech_web/webui/src/features/challenge/ChallengeNodeCard.tsx
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
            // 根は Paper の面。名前は testid、アンカーはチャレンジの GUID
            // The root is the Paper face; its name is the testid and its anchor is the challenge GUID
            var key = challengeGuid.ToString();
            Root = new PaperSurface($"challenge-node-{key}");
            Root.AddToClassList("challenge-list__card");
            TutorialAnchor.Mark(Root, AnchorIds.ChallengeNode(key));

            // 名前（太字 md）・概要（sm）・状態（xs）を縦に積む。名前と概要はチャレンジで決まる
            // Stack title (bold md), summary (sm) and state (xs); title and summary are fixed per challenge
            var title = AddText(UiTypographyClass.TextMd, "challenge-list__card-title");
            title.AddToClassList(UiTypographyClass.TextBold);
            title.SetText(UiText.FromContent(ContentLocalizationKeys.ChallengeTitle(challengeGuid)));
            var summary = AddText(UiTypographyClass.TextSm, "challenge-list__card-summary");
            summary.SetText(UiText.FromContent(ContentLocalizationKeys.ChallengeSummary(challengeGuid)));
            _state = AddText(UiTypographyClass.TextXs, "challenge-list__card-state");

            // 輪は Web の outline。拾わない子を最後に重ねる
            // The ring is the Web outline; a non-picking child laid on top last
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
            // 状態は修飾クラスと State が決めた文言キー、Web の scale(UIScale) は style の scale（中心を軸に拡縮）
            // State becomes a modifier class and the label key State chose; the Web scale(UIScale) goes to style scale (about the center)
            Root.EnableInClassList("challenge-list__card--locked", node.State == ChallengeNodeState.Locked);
            Root.EnableInClassList("challenge-list__card--current", node.State == ChallengeNodeState.Current);
            Root.EnableInClassList("challenge-list__card--completed", node.State == ChallengeNodeState.Completed);
            _state.SetText(new UiText(node.StateLabelKey));
            Root.style.scale = new Scale(new Vector2(node.Scale.x, node.Scale.y));
        }
    }
}
