namespace Game.Block.Interface.Component
{
    public interface IBlockSaveState : IBlockComponent
    {
        public string SaveKey { get; }
        
        // 新規に組み立てた、生きたコレクション参照を含まないオブジェクトを返す（JSON化は別スレッドで行われる）
        // Return a freshly built object with no live collection references (serialization happens on another thread)
        object GetSaveState();
    }
}
