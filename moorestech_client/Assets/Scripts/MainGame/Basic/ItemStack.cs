namespace MainGame.Basic
{
    public struct ItemStack
    {
        public int ID;
        public int Count;

        public ItemStack(int id = default, int count = default)
        {
            ID = id;
            Count = count;
        }
    }
}