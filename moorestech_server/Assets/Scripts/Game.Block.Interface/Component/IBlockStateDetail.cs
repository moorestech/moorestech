namespace Game.Block.Interface.Component
{
    public interface IBlockStateDetail : IBlockComponent 
    {
        /// <summary>
        /// 抽象クラスを返すのではなく具体的なクラスを返す理由は、MessagePackの変換が抽象クラスだと上手くいかないため
        /// TODO Convert.ChangeTypeを使っても良いかもしれない、、要検討
        /// The reason for returning a concrete class instead of an abstract class is that MessagePack conversions do not work with abstract classes.
        /// TODO It may be better to use Convert.ChangeType.
        /// </summary>
        /// <returns></returns>
        public BlockStateDetail[] GetBlockStateDetails();
    }
    
    public struct BlockStateDetail
    {
        public string Key { get; }
        public byte[] Value { get; }
        
        public BlockStateDetail(string key, byte[] value)
        {
            Key = key;
            Value = value;
        }
    }
}