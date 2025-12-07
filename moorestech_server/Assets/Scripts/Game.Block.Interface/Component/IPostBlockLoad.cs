namespace Game.Block.Interface.Component
{
    // 全てのブロックのロードが終了した後に呼び出されるインターフェース
    // Interface called after all blocks are loaded
    public interface IPostBlockLoad : IBlockComponent
    {
        // 全てのブロックがロードされた後に呼び出される
        // Called after all blocks are loaded
        void OnPostBlockLoad();
    }
}

