using Game.Block.Interface;

namespace Client.Game.InGame.Block.Removal
{
    /// <summary>
    ///     撤去したブロックを同じ状態で再設置するための生成パラメータを渡す
    ///     Supplies creation parameters for replacing a removed block in the same state
    /// </summary>
    public interface IBlockRecreateParamSource
    {
        BlockCreateParam[] GetBlockRecreateParams();
    }
}
