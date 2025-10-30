namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail
{
    /// <summary>
    /// ユーザー側からの見た目はこの方向でレールが設置される。実態はVector3で表現される。
    /// Rail is placed in this direction as seen from the user. The actual representation is in Vector3.
    /// </summary>
    public enum RailComponentDirection
    {
        Direction0,
        Direction45,
        Direction90,
        Direction135,
        Direction180,
        Direction225,
        Direction270,
        Direction315,
        Direction330,
        Direction345,
    }
}