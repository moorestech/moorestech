namespace MainGame.UnityView.UI.Builder.Element
{
    public class UIBluePrintItemSlotArray : IUIBluePrintElement
    {

        public UIBluePrintElementType ElementElementType => UIBluePrintElementType.ArraySlot;
        public int Priority { get; }
        public string IdName { get; }
        public readonly int BottomBlank;

        public readonly float X;
        public readonly float Y;

        public readonly int Height;
        public readonly int Width;

        public UIBluePrintItemSlotArray(float x, float y, int priority, int height, int width,  int bottomBlank = 0,string idName = "")
        {
            X = x;
            Y = y;
            Priority = priority;
            BottomBlank = bottomBlank;
            Height = height;
            Width = width;
            IdName = idName;
        }
    }
}