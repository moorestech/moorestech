using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.DistanceCulling;

namespace Client.Game.InGame.Block.GearRotation
{
    /// <summary>
    /// 半径内/外の指示を歯車の回転更新の有効状態へ渡す。状態受信は無効中も続く
    /// Forwards within/outside-radius instructions to gear rotation updates; state reception continues while disabled
    /// </summary>
    public sealed class GearRotationCullingTarget : IDistanceCullingTarget
    {
        private readonly GearStateChangeProcessor[] _processors;

        public GearRotationCullingTarget(GearStateChangeProcessor[] processors)
        {
            _processors = processors;
        }

        public void SetWithinRange(bool withinRange)
        {
            foreach (var processor in _processors) processor.enabled = withinRange;
        }
    }
}
