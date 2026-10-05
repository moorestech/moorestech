using Game.Block.Interface;

namespace Client.Game.InGame.Block.Removal
{
    /// <summary>
    ///     撤去したブロックを同じ状態で再設置するための生成パラメータを渡す
    ///     Supplies creation parameters for replacing a removed block in the same state
    /// </summary>
    public interface IBlockRecreateParamSource
    {
        // 初期状態が未着なら false。生成値ゼロ件は成功として true を返す
        // Returns false while the initial state has not arrived; zero params is a valid success
        bool TryGetBlockRecreateParams(out BlockCreateParam[] createParams);
    }
}
