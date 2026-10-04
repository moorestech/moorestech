using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;

namespace Client.Game.InGame.UI.UIState.State.PlacementPick
{
    /// <summary>
    ///     接続線スポイトの結果。不成立の理由（種類が解放状態に無い／未解放）を区別する
    ///     Result of the connection-line eyedropper; distinguishes why it failed (tool unknown to the unlock state / locked)
    /// </summary>
    public enum ConnectionLinePickOutcome
    {
        Picked,
        UnknownTool,
        Locked,
    }

    public readonly struct ConnectionLinePickResult
    {
        public ConnectionLinePickOutcome Outcome { get; }
        public IPlacementTarget Target { get; }

        private ConnectionLinePickResult(ConnectionLinePickOutcome outcome, IPlacementTarget target)
        {
            Outcome = outcome;
            Target = target;
        }

        public static ConnectionLinePickResult Picked(IPlacementTarget target)
        {
            return new ConnectionLinePickResult(ConnectionLinePickOutcome.Picked, target);
        }

        public static ConnectionLinePickResult Failed(ConnectionLinePickOutcome outcome)
        {
            return new ConnectionLinePickResult(outcome, null);
        }
    }
}
