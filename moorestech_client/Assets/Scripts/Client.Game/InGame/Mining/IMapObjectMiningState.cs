namespace Client.Game.InGame.Mining
{
    public interface IMapObjectMiningState
    {
        IMapObjectMiningState GetNextUpdate(MapObjectMiningControllerContext context, float dt);
    }
}
