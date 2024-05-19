namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    /// <summary>
    ///     無機能のブロックに使うステートプロセッサー
    /// </summary>
    public class NullBlockStateChangeProcessor : IBlockStateChangeProcessor
    {
        public void OnChangeState(string currentState, string previousState, byte[] currentStateData)
        {
        }
    }
}