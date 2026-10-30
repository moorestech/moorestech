using System.Linq;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using MessagePack;
using NUnit.Framework;
using UniRx;

namespace Tests.CombinedTest.Core.Machine
{
    // 状態イベントごとに、その時点の電力・公開状態・理由を記録する
    // Record the power, published state and reason read at each state event
    internal sealed class MachinePowerNoticeRecorder
    {
        public int Count { get; private set; }
        public float LastCurrentPower { get; private set; }
        public float LastRequestPower { get; private set; }
        public string LastStateType { get; private set; }
        public bool LastOutputSpaceShort { get; private set; }

        public static MachinePowerNoticeRecorder Subscribe(IBlockStateObservable observable)
        {
            var recorder = new MachinePowerNoticeRecorder();
            observable.OnChangeBlockState.Subscribe(_ => recorder.Record(observable.GetBlockStateDetails()));
            return recorder;
        }

        // 区間内に通知が出て、最後の通知が期待の分子分母・idle・理由ありを持つことを確かめる
        // Assert the span notified and its last notice carries the expected power pair, idle and the reason
        public void AssertLastNotice(int countBeforeSpan, float expectedCurrentPower, float expectedRequestPower, string span)
        {
            Assert.Less(countBeforeSpan, Count, $"{span}: 出力詰まり中に状態イベントが出ていない");
            Assert.AreEqual(expectedCurrentPower, LastCurrentPower, 0.01f, $"{span}: 最後の通知の供給電力");
            Assert.AreEqual(expectedRequestPower, LastRequestPower, 0.01f, $"{span}: 最後の通知の要求電力");
            Assert.AreEqual("idle", LastStateType, $"{span}: 公開状態");
            Assert.IsTrue(LastOutputSpaceShort, $"{span}: 出力空き不足の理由");
        }

        private void Record(BlockStateDetail[] details)
        {
            var common = MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(details.First(d => d.Key == CommonMachineBlockStateDetail.BlockStateDetailKey).Value);
            var machine = MessagePackSerializer.Deserialize<MachineBlockStateDetail>(details.First(d => d.Key == MachineBlockStateDetail.BlockStateDetailKey).Value);
            Count++;
            LastCurrentPower = common.CurrentPower;
            LastRequestPower = common.RequestPower;
            LastStateType = common.CurrentStateType;
            LastOutputSpaceShort = machine.IsOutputSpaceShort;
        }
    }
}
