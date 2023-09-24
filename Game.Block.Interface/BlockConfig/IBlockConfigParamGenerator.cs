namespace Game.Block.Interface.BlockConfig
{
    public interface IBlockConfigParamGenerator
    {
        public IBlockConfigParam Generate(dynamic blockParam);
    }
}