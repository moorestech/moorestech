using MainGame.ModLoader.Glb;

namespace MainGame.UnityView.Block.StateChange
{
    /// <summary>
    ///     無機能のブロックに使うステートプロセッサー
    /// </summary>
    public class NullBlockStateChangeProcessor : IBlockStateChangeProcessor
    {
        public void OnChangeState(string currentState, string previousState, string currentStateData)
        {
        }
    }
}