using MessagePack;

namespace Game.Block.Interface.Component
{
    public interface IBlockDebugInfo : IBlockComponent
    {
        public BlockDebugInfo GetDebugInfo();
    }
    
    
    [MessagePackObject]
    public struct BlockDebugInfo
    {
        [Key(0)] public string ComponentName;
        [Key(1)] public string Value;
    }
}