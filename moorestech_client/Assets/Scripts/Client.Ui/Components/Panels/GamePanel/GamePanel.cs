using Client.Ui.Foundation.Theme;
using System;
using Client.Ui.Components.Decor;
using Client.Ui.State.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Panels
{
    /// <summary>
    /// 枠付きのゲームパネル（default・craft）。題・題の右端の副次アクション・本文を持つ（Web の shared/ui/GamePanel の額縁つき variant）
    /// Framed game panel (default, craft) with a title, a trailing title action and a body (the framed variants of Web shared/ui/GamePanel)
    /// </summary>
    /// <remarks>
    /// 面だけの hud・skit は GamePanelSurface に分け、題の API を型で到達不能にする（Web の FramedProps と FaceOnlyProps）。外周の影は描かない（C2-a 判断記録 D-1）
    /// Face-only hud and skit live in GamePanelSurface so title APIs are unreachable by type (Web FramedProps and FaceOnlyProps); the outer shadow is not drawn (C2-a decision D-1)
    /// </remarks>
    public sealed class GamePanel : VisualElement
    {
        private readonly VisualElement _header;
        private readonly VisualElement _titleAction;
        private readonly VisualElement _body;
        private GamePanelTitle _title;

        public override VisualElement contentContainer => _body;

        public GamePanel(string testid, GamePanelFrame frame)
        {
            name = testid;
            AddToClassList("game-panel");
            _header = new VisualElement();
            _header.AddToClassList("game-panel__header");
            _titleAction = new VisualElement { pickingMode = PickingMode.Ignore };
            _titleAction.AddToClassList("game-panel__title-action");
            _header.Add(_titleAction);
            _body = new VisualElement();
            _body.AddToClassList("game-panel__body");

            // 額縁ごとの子の並びを 1 か所で決める。背後の面と装飾（Web の z-index 0）を先に、craft のグリップ（z-index 2）を最後に置く
            // Decide each frame's child order in one place: backdrop (Web z-index 0) first, the craft grip (z-index 2) last
            switch (frame)
            {
                case GamePanelFrame.Default:
                    AddToClassList("game-panel--default");
                    hierarchy.Add(new GamePanelFace("game-panel__face"));
                    hierarchy.Add(new GamePanelBottomDeco());
                    AddTitleRowsAndBody();
                    break;
                case GamePanelFrame.Craft:
                    // 面はパネル自身の背景（USS）。輪は線の内縁を基準にするため線の子にする
                    // The face is the panel's own background (USS); the ring is the line's child so it measures from the line's inner edge
                    AddToClassList("game-panel--craft");
                    var craftLine = Decoration("game-panel__craft-line");
                    craftLine.Add(Decoration("game-panel__craft-rim"));
                    hierarchy.Add(craftLine);
                    AddTitleRowsAndBody();
                    hierarchy.Add(new GamePanelCraftGrip());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(frame), frame, null);
            }

            #region Internal

            // --titled が付くまで題と罫線を隠す
            // Hide title and rules until --titled is added
            void AddTitleRowsAndBody()
            {
                hierarchy.Add(DecoLine("game-panel__deco-line--top"));
                hierarchy.Add(_header);
                hierarchy.Add(DecoLine("game-panel__deco-line--bottom"));
                hierarchy.Add(_body);
            }

            // 題の罫線は FadeRule と同じ要素で、色は位置のクラスが --game-panel-rule-* で渡す（Web の .decoLine）
            // Title rules reuse the FadeRule element; the position class passes colors via --game-panel-rule-* (the Web .decoLine)
            VisualElement DecoLine(string positionClassName)
            {
                var line = new FadeGradientRule("--game-panel-rule-start", "--game-panel-rule-peak", "--game-panel-rule-end");
                line.AddToClassList("game-panel__deco-line");
                line.AddToClassList(positionClassName);
                return line;
            }

            VisualElement Decoration(string className)
            {
                var decoration = new VisualElement { pickingMode = PickingMode.Ignore };
                decoration.AddToClassList(className);
                return decoration;
            }

            #endregion
        }

        public void SetTitle(UiText title)
        {
            // 題は初回にだけ作る。キー無しの LocalizedLabel をパネルに載せない（載せると LogError になる）
            // Create the title only once; never attach a keyless LocalizedLabel (that logs an error)
            if (_title == null)
            {
                _title = new GamePanelTitle();
                _title.SetText(title);
                _header.Insert(0, _title);
            }
            else
            {
                _title.SetText(title);
            }
            AddToClassList("game-panel--titled");
        }

        public void SetTitleAction(VisualElement action)
        {
            // 題が無いと題の行ごと隠れる（Web も title 無しでは titleAction を描かない）。出さないまま誤用をログへ出す
            // Without a title the whole row is hidden (the Web also skips titleAction without a title); keep it hidden but log the misuse
            if (_title == null)
            {
                Debug.LogError($"GamePanel.SetTitleAction は SetTitle の後に呼ぶ（題が無いと題の行ごと隠れる）: {name}");
                return;
            }

            // 副次アクションは 1 つだけ。差し替えは前の要素を外してから足す
            // Only one title action; replacing removes the previous element first
            _titleAction.Clear();
            _titleAction.Add(action);
        }
    }
}
