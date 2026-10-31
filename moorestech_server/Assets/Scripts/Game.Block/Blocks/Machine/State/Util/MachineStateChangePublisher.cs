using System;
using UniRx;

namespace Game.Block.Blocks.Machine.State.Util
{
    // 通常・清浄室機械の状態通知
    // 前回通知状態を保持
    // 遷移・加工・詰まり中に通知
    // State notices for normal and clean-room machines
    // Retain the last notified state
    // Notify on transitions, processing and output blockage
    internal sealed class MachineStateChangePublisher
    {
        public IObservable<Unit> OnChangeBlockState => _changeState;
        private readonly Subject<Unit> _changeState = new();

        // 状態詳細へ「直前の状態」として載る値。通知の後に更新するので、購読側は通知時点で遷移前の状態を読める
        // Published as the previous state in the detail; updated after notifying so subscribers read the pre-transition state at notice time
        public ProcessState LastState { get; private set; } = ProcessState.Idle;

        // tick外で状態を書き換えた操作や給電の直後に、変化の有無を問わず通知する
        // Notify unconditionally right after an off-tick state rewrite or a power supply
        public void Publish()
        {
            _changeState.OnNext(Unit.Default);
        }

        // tick内の遷移の後、状態変化時か毎tick詳細が動く状態なら通知して記録を進める
        // After the tick's transition, notify on a state change or in a state whose detail moves every tick, then advance the record
        public void PublishIfStateChangedOrDetailVaries(ProcessState currentState)
        {
            if (LastState == currentState && !IsDetailVaryingEveryTick()) return;
            PublishTickState(currentState);

            #region Internal

            // 加工中は進捗と電力、出力詰まり中は電力が毎tickラッチで動く。給電時の再通知はIdleだけなので、ここで運ばないと詰まり中の電力が固着する
            // Processing moves progress and power, output blockage moves power with every tick's latch; supply re-notifies only while idle, so without this the blocked power freezes
            bool IsDetailVaryingEveryTick()
            {
                return currentState == ProcessState.Processing || currentState == ProcessState.OutputBlocked;
            }

            #endregion
        }

        // tick内で詳細が確定した時点の通知。記録も進めるので、同じtickの遷移判定が重ねて通知しない
        // The notice once the tick's detail settles; it advances the record so the same tick's transition check does not notify again
        public void PublishTickState(ProcessState currentState)
        {
            _changeState.OnNext(Unit.Default);
            LastState = currentState;
        }
    }
}
