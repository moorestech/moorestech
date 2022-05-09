namespace MainGame.UnityView.UI.Inventory.View.SubInventory.Element
{
    public class ArraySlot : ISubInventoryElement
    {

        public SubInventoryElementType ElementType => SubInventoryElementType.ArraySlot;
        public int Priority { get; }

        public readonly float X;
        public readonly float Y;

        public readonly int Height;
        public readonly int Width;
        
        public ArraySlot(float x, float y, int priority, int height, int width)
        {
            X = x;
            Y = y;
            Priority = priority;
            Height = height;
            Width = width;
        }
    }
}