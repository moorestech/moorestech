namespace Game.Block.Interface.Component
{
    public interface IReceiveCreateParam : IBlockComponent
    {
        /// <summary>
        /// ブロック作成時にパラメーターを受け取ります。
        /// Parameters are received when creating a block.
        /// </summary>
        public void OnCreate(BlockCreateParam[] blockCreateParams);
    }
}