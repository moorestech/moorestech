namespace Game.Block.Interface.Component
{
    public interface IBlockComponent
    {
        public bool IsDestroy { get; }

        public void Destroy();
    }
}