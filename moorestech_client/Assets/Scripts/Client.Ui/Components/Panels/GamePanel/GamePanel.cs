using UnityEngine.UIElements;

namespace Client.Ui.Components.Panels
{
    /// <summary>
    /// ゲームの窓枠（default）。半透明ネイビーの面・タイトル行と罫線・本文・下部の三角を持つ（plan.md §3.4）
    /// Game window frame (default) with a translucent navy face, title row with rules, body and bottom triangles (plan.md §3.4)
    /// </summary>
    public class GamePanel : VisualElement
    {
        private readonly VisualElement _topDecoLine;
        private readonly VisualElement _header;
        private readonly VisualElement _bottomDecoLine;
        private readonly VisualElement _body;

        public override VisualElement contentContainer => _body;

        public GamePanel()
        {
            AddToClassList("game-panel");

            // 面と三角は Web の z-index 0 なので本文より先（背後）に置く
            // The face and triangles are z-index 0 on the Web, so they go first (behind the content)
            hierarchy.Add(new GamePanelFace());
            hierarchy.Add(new GamePanelBottomDeco());

            // タイトル行は SetTitle まで隠し、罫線もそれに合わせる
            // Keep the title row and its rules hidden until SetTitle
            _topDecoLine = new GamePanelDecoLine("game-panel__deco-line--top");
            _topDecoLine.style.display = DisplayStyle.None;
            hierarchy.Add(_topDecoLine);
            _header = new VisualElement();
            _header.AddToClassList("game-panel__header");
            _header.style.display = DisplayStyle.None;
            hierarchy.Add(_header);
            _bottomDecoLine = new GamePanelDecoLine("game-panel__deco-line--bottom");
            _bottomDecoLine.style.display = DisplayStyle.None;
            hierarchy.Add(_bottomDecoLine);

            _body = new VisualElement();
            _body.AddToClassList("game-panel__body");
            hierarchy.Add(_body);
        }

        public void SetTitle(Label titleLabel)
        {
            titleLabel.AddToClassList("game-panel__title");
            _header.Insert(0, titleLabel);
            _topDecoLine.style.display = DisplayStyle.Flex;
            _header.style.display = DisplayStyle.Flex;
            _bottomDecoLine.style.display = DisplayStyle.Flex;
        }
    }
}
