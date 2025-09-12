using Client.Game.InGame.Block;
using Server.Event.EventReceive;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    /// <summary>
    ///     無機能のブロックに使うステートプロセッサー
    /// </summary>
    public class NullBlockStateChangeProcessor : IBlockStateChangeProcessor
    {
        public void Initialize(BlockGameObject blockGameObject) { }
        public void OnChangeState(BlockStateMessagePack blockState)
        {
        }
    }
}