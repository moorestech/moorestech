namespace Game.Block.Interface.Component.WorldMutation
{
    public interface IBlockWorldMutationParticipant : IBlockComponent
    {
        IBlockWorldMutation CaptureWorldMutation();
    }

    public interface IBlockWorldMutation
    {
        void ApplyAfterMutation();
    }
}
