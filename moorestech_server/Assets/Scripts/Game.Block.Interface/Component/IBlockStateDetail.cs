namespace Game.Block.Interface.Component
{
    public interface IBlockStateDetail : IBlockComponent 
    {
        /// <summary>
        /// ブロックの状態詳細を取得します。
        /// BlockStateDetail[]は複数のコンポーネントからデータを収集することを目的としているため、
        /// 各コンポーネントは自身の状態を表す単一のBlockStateDetailを含む配列を返す必要があります。
        /// 
        /// 抽象クラスを返すのではなく具体的なクラスを返す理由は、MessagePackの変換が抽象クラスだと上手くいかないため
        /// TODO Convert.ChangeTypeを使っても良いかもしれない、、要検討
        /// 
        /// Gets the block state details.
        /// Since BlockStateDetail[] is intended to collect data from multiple components,
        /// each component should return an array containing a single BlockStateDetail representing its own state.
        /// 
        /// The reason for returning a concrete class instead of an abstract class is that MessagePack conversions do not work with abstract classes.
        /// TODO It may be better to use Convert.ChangeType.
        /// </summary>
        /// <returns>単一のBlockStateDetailを含む配列 / An array containing a single BlockStateDetail</returns>
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