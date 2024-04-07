namespace Core.Item.Config
{
    public interface IItemConfigData
    {
        public long ItemHash { get; }
        public int ItemId { get; }
        
        public string ModId { get; }
        public string Name { get; }
        
        public int MaxStack { get; }
        public string ImagePath { get; }
    }
}