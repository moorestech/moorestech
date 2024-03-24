namespace Game.Block.Interface
{
    public interface IBlockComponent
    {
        public bool IsDestroy { get; }

        public void Destroy();
    }
}