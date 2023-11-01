using UnityEngine;

namespace MainGame.UnityView.UI.Builder.Element
{
    public class UIBluePrintProgressArrow : IUIBluePrintElement
    {
        public UIBluePrintElementType ElementElementType => UIBluePrintElementType.ProgressArrow;
        public int Priority { get; }
        public string IdName { get; }
        public Vector2 RectPosition { get; }
        public Vector3 Rotation { get; }
        public Vector2 RectSize { get; }


        
        public UIBluePrintProgressArrow (int priority, string idName, Vector2 rectPosition, Vector3 rotation, Vector2 size)
        {
            Priority = priority;
            IdName = idName;
            RectPosition = rectPosition;
            Rotation = rotation;
            RectSize = size;
        }
    }
}