using System;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using UniRx;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    // 部屋から押し込まれた稼働可否を保持し、変化したときだけブロック状態として通知する
    // Holds the operability pushed by the room and notifies a block state change only when it changes
    public class CleanRoomOperabilityStateComponent : IBlockStateObservable
    {
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;
        private readonly Subject<Unit> _onChangeBlockState = new();

        // 部屋の効果が届く前は、効果の既定値と同じく未密閉として扱う
        // Before any room effect arrives, treat the machine as not enclosed like the effect default
        public CleanRoomOperability Operability { get; private set; } = CleanRoomOperability.NotEnclosed;

        public void SetOperability(CleanRoomOperability operability)
        {
            BlockException.CheckDestroy(this);

            // 毎tick同じ値が届くため、変化したときだけ通知する
            // The same value arrives every tick, so notify only on change
            if (Operability == operability) return;
            Operability = operability;
            _onChangeBlockState.OnNext(Unit.Default);
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);
            return new[] { CleanRoomOperabilityStateDetail.CreateState(Operability) };
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
            _onChangeBlockState.Dispose();
        }
    }
}
