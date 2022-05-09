namespace MainGame.UnityView.UI.Inventory.View.SubInventory.Element
{
    public class ArraySlot : ISubInventoryElement
    {
        public ArraySlot(float x, float y, int priority, int height, int width)
        {
            X = x;
            Y = y;
            this.priority = priority;
            Height = height;
            Width = width;
        }

        public SubInventoryElementType ElementType => SubInventoryElementType.ArraySlot;
        public int Priority => priority;
        
        public float X;
        public float Y;
        public int priority;
        
        public int Height;
        public int Width;
    }
}