using UnityEngine.UIElements;

namespace Client.Ui.Components.Panels
{
    /// <summary>
    /// ゲームの窓枠（default）
    /// Game window frame (default)
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

            // 面と三角は本文の背後
            // Face and triangles sit behind content
            hierarchy.Add(new GamePanelFace());
            hierarchy.Add(new GamePanelBottomDeco());

            // タイトル行はSetTitleまで隠す
            // Title row hidden until SetTitle
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
