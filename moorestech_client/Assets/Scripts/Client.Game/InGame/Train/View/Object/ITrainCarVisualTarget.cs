namespace Client.Game.InGame.Train.View.Object
{
    public interface ITrainCarVisualTarget
    {
        bool UpdateVisual(TrainCarRailPositionVisualState visualState);

        void SetMaterialMode(TrainCarVisualMaterialMode materialMode);

        void RequestOverlayForCurrentFrame(TrainCarVisualMaterialMode materialMode);

        void DestroyRuntimeMaterials();
    }
}
