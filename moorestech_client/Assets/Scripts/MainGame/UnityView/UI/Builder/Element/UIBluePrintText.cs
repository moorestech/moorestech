using UnityEngine;

namespace MainGame.UnityView.UI.Builder.Element
{
    public class UIBluePrintText : IUIBluePrintElement
    {
        public readonly string DefaultText;
        public readonly int FontSize;

        public UIBluePrintText(int priority, string defaultText, int fontSize, Vector2 rectPosition, Vector3 rotation, Vector2 size, string idName = "")
        {
            Priority = priority;
            DefaultText = defaultText;
            FontSize = fontSize;
            RectPosition = rectPosition;
            Rotation = rotation;
            RectSize = size;
            IdName = idName;
        }

        public UIBluePrintElementType ElementElementType => UIBluePrintElementType.Text;
        public int Priority { get; }
        public string IdName { get; }
        public Vector2 RectPosition { get; }
        public Vector3 Rotation { get; }
        public Vector2 RectSize { get; }
    }
}