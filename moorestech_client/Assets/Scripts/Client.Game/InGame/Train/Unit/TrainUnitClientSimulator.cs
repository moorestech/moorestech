using Client.Game.InGame.Train.View;
using UnityEngine;
using VContainer.Unity;
using Client.Game.InGame.Train.Network.TickSynchronization;

namespace Client.Game.InGame.Train.Unit
{
    public sealed class TrainUnitClientSimulator : ITickable
    {
        private readonly TrainTickContext _context;
        private readonly TrainUnitTickState _tickState;
        private readonly ITrainUnitHashTickGate _hashTickGate;
        private readonly TrainUnitVisualUpdateSystem _visualUpdateSystem;

        public TrainUnitClientSimulator(TrainTickContext context, TrainUnitHashVerifier hashTickGate, TrainUnitVisualUpdateSystem visualUpdateSystem)
        {
            _context = context;
            _tickState = context.State;
            _hashTickGate = hashTickGate;
            _visualUpdateSystem = visualUpdateSystem;
        }

        public void Tick()
        {
            if (!_context.IsInitialSnapshotApplied || _tickState.IsStopped) return;
            // 進行後のtickで列車表示を更新。
            // Update train visuals at the advanced tick.
            var renderTick = _context.AdvanceController.Advance(Time.deltaTime, _hashTickGate);
            if (!_tickState.IsStopped)
                _visualUpdateSystem.UpdateAll(renderTick, _tickState.GetTick());
        }
    }
}
