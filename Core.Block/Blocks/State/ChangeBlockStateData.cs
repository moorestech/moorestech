using MessagePack;

namespace Core.Block.Blocks.State
{
    /// <summary>
    /// 各ブロックのステートを表現するクラスです
    /// このクラスは必ず継承して使ってください
    /// </summary>
    [MessagePackObject(keyAsPropertyName :true)]
    public class ChangeBlockStateData{}
}