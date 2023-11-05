using MainGame.UnityView.UI.Builder.BluePrint;
using UnityEngine;

namespace MainGame.UnityView.UI.Builder.Element
{
    public class UIBluePrintItemSlot : IUIBluePrintElement
    {
        public static readonly Vector2 DefaultItemSlotRectSize = new(100, 100);


        public readonly InventorySlotElementOptions Options;

        public UIBluePrintItemSlot(int priority, InventorySlotElementOptions options, Vector2 rectPosition, Vector3 rotation, Vector2 size, string idName = "")
        {
            Priority = priority;
            Options = options;
            RectPosition = rectPosition;
            Rotation = rotation;
            RectSize = size;
            IdName = idName;
        }


        public UIBluePrintElementType ElementElementType => UIBluePrintElementType.OneSlot;
        public int Priority { get; }
        public string IdName { get; }
        public Vector2 RectPosition { get; }
        public Vector3 Rotation { get; }
        public Vector2 RectSize { get; }
    }
}