namespace Client.Game.InGame.Train.View.Object
{
    public interface ITrainCarPoseUpdater
    {
        bool UpdatePose(TrainCarRailPositionVisualState visualState);
        bool CollectPoseRequests(TrainCarRailPositionVisualState visualState, TrainCarRailPositionPoseBatch poseBatch);
        bool ApplyBatchedPose(TrainCarRailPositionVisualState visualState, TrainCarRailPositionPoseBatch poseBatch);
    }
}
