using Client.Game.InGame.Block;
using Server.Event.EventReceive;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    /// <summary>
    ///     ステートの変更を受け取り、その変更に合わせてアニメーション、エフェクト、音を再生するためのinterface
    ///     各ブロックやタイプによって実行する内容が違うため、各ブロックやタイプで実装する
    /// </summary>
    public interface IBlockStateChangeProcessor : IBlockGameObjectInnerComponent
    {
        /// <summary>
        ///     ブロックのステートに基づいてアニメーションを再生する
        ///     タイプに応じてアニメーションを再生する
        /// </summary>
        public void OnChangeState(BlockStateMessagePack blockState);
    }
}