namespace Game.Block.Interface.State
{
    /// <summary>
    ///     
    ///     <see cref="CurrentState" /><see cref="PreviousState" />String
    ///     
    ///     TODO 
    /// </summary>
    public class ChangedBlockState
    {
        public readonly string CurrentState;
        public readonly string CurrentStateJsonData;
        public readonly string PreviousState;

        public ChangedBlockState(string currentState, string previousState, string currentStateJsonData = null)
        {
            CurrentState = currentState;
            PreviousState = previousState;
            CurrentStateJsonData = currentStateJsonData;
        }
    }
}