using UnityEngine;

namespace MainGame.UnityView.UI.Builder.Element
{
    public class UIBluePrintItemSlotArray : IUIBluePrintElement
    {

        public UIBluePrintElementType ElementElementType => UIBluePrintElementType.ArraySlot;
        public int Priority { get; }
        public string IdName { get; }
        public Vector2 RectPosition { get; }
        public Vector3 Rotation { get; }
        public Vector2 RectSize { get; }
        
        public readonly int BottomBlank;


        public readonly int ArrayRow;
        public readonly int ArrayColumn;

        public UIBluePrintItemSlotArray(int priority, int arrayRow, int arrayColumn, Vector2 rectPosition, Vector3 rotation, Vector2 size, int bottomBlank = 0,string idName = "")
        {
            Priority = priority;
            BottomBlank = bottomBlank;
            ArrayRow = arrayRow;
            ArrayColumn = arrayColumn;
            RectPosition = rectPosition;
            Rotation = rotation;
            RectSize = size;
            IdName = idName;
        }

        public UIBluePrintItemSlotArray(int priority, int arrayRow, int arrayColumn,Vector2 rectPosition)
        {
            
            Priority = priority;
            ArrayRow = arrayRow;
            ArrayColumn = arrayColumn;
            RectPosition = rectPosition;
            
            RectSize = UIBluePrintItemSlot.DefaultItemSlotRectSize * new Vector2(arrayColumn, arrayRow);
            
            BottomBlank = 0;
            Rotation = Vector3.zero;
            IdName = "";
        }
    }
}