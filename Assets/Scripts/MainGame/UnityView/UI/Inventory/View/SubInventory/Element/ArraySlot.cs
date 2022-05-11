namespace MainGame.UnityView.UI.Inventory.View.SubInventory.Element
{
    public class ArraySlot : ISubInventoryElement
    {

        public SubInventoryElementType ElementType => SubInventoryElementType.ArraySlot;
        public int Priority { get; }
        public readonly int BottomBlank;

        public readonly float X;
        public readonly float Y;

        public readonly int Height;
        public readonly int Width;

        public ArraySlot(float x, float y, int priority, int height, int width, int bottomBlank = 0)
        {
            X = x;
            Y = y;
            Priority = priority;
            BottomBlank = bottomBlank;
            Height = height;
            Width = width;
        }
    }
}