using UnityEngine;

namespace MainGame.UnityView.UI.Builder.Element
{
    public interface IUIBluePrintElement
    {
        public UIBluePrintElementType ElementElementType { get; }
        public int Priority { get; }

        /// <summary>
        ///     そのUI要素の名前
        ///     必須ではないが、そのUI要素を特定するために使う
        /// </summary>
        public string IdName { get; }

        public Vector2 RectPosition { get; }
        public Vector3 Rotation { get; }
        public Vector2 RectSize { get; }
    }
}