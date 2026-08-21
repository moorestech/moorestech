namespace Client.Game.InGame.Mining
{
    public interface IMiningState
    {
        IMiningState GetNextUpdate(MiningControllerContext context, float dt);
    }
}
