using Client.Game.InGame.Block;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    /// <summary>
    /// ブロックオブジェクトの内部で何らかの処理をする、共通のinterface
    /// Common interface for components that perform certain processing within a BlockGameObject
    /// </summary>
    public interface IBlockGameObjectInnerComponent
    {
        /// <summary>
        /// 自身が処理するBlockGameObjectを注入する
        /// Injects the BlockGameObject that this component will process
        /// </summary>
        public void Initialize(BlockGameObject blockGameObject);
    }
}