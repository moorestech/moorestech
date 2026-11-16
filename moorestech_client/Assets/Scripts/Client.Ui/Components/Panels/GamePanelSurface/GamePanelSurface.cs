using System;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Panels
{
    /// <summary>
    /// 枠を持たない面だけのパネル（hud・skit）。題を持たないことを型で分ける（Web の GamePanel の FaceOnlyProps）
    /// Face-only panel without a frame (hud, skit); the type keeps titles unreachable (the Web GamePanel FaceOnlyProps)
    /// </summary>
    public sealed class GamePanelSurface : VisualElement
    {
        private readonly VisualElement _body;

        public override VisualElement contentContainer => _body;

        public GamePanelSurface(string testid, GamePanelSurfaceKind kind)
        {
            name = testid;
            AddToClassList("game-panel-surface");
            AddToClassList(KindClassName(kind));

            // 面は本文の背後。色とフェードは種類のクラスが USS で渡す
            // The face sits behind the body; the kind class passes its color and fades through USS
            hierarchy.Add(new GamePanelFace("game-panel-surface__face"));
            _body = new VisualElement();
            _body.AddToClassList("game-panel-surface__body");
            hierarchy.Add(_body);

            #region Internal

            static string KindClassName(GamePanelSurfaceKind kind)
            {
                return kind switch
                {
                    GamePanelSurfaceKind.Hud => "game-panel-surface--hud",
                    GamePanelSurfaceKind.Skit => "game-panel-surface--skit",
                    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
                };
            }

            #endregion
        }
    }
}
