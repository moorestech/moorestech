using System.Linq;
using System.Threading;
using Client.Tests.EditModeInPlayingTest.Util;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Orbit;
using Game.SpaceElevator;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;

namespace Client.Tests.EditModeInPlayingTest.BlockInventory
{
    internal sealed class SpaceElevatorServerTestState : ServerTestAction
    {
        private readonly IBlock _target;
        private readonly IBlock _decoy;
        internal int PoolAmount { get; private set; }
        internal bool TargetConnected { get; private set; }
        internal bool DecoyConnected { get; private set; }

        private SpaceElevatorServerTestState(IBlock target, IBlock decoy)
        {
            _target = target;
            _decoy = decoy;
        }

        internal static async UniTask<SpaceElevatorServerTestState> ReadAsync(IBlock target, IBlock decoy)
        {
            var state = new SpaceElevatorServerTestState(target, decoy);
            await state.ExecuteAsync();
            return state;
        }

        internal static async UniTask<int> SupplyCableAsync(SpaceElevatorBaseBlockParam param)
        {
            var callerThread = Thread.CurrentThread.ManagedThreadId;
            var action = new CableArrival(param);
            await action.ExecuteAsync();
            Assert.AreNotEqual(callerThread, action.ServerThreadId, "arrival must run on the server thread");
            Assert.GreaterOrEqual(action.Amount, param.CablesPerElevator);
            return action.Amount;
        }

        protected override void ExecuteOnServer()
        {
            // 期待値用の状態も同じ所有スレッドで読む
            // Read assertion snapshots on the owning thread too
            PoolAmount = ServerContext.GetService<SpaceElevatorCablePool>().Amount;
            TargetConnected = _target.GetComponent<SpaceElevatorClimberComponent>().IsConnected;
            DecoyConnected = _decoy.GetComponent<SpaceElevatorClimberComponent>().IsConnected;
        }

        private sealed class CableArrival : ServerTestAction
        {
            private readonly SpaceElevatorBaseBlockParam _param;
            internal int Amount { get; private set; }

            internal CableArrival(SpaceElevatorBaseBlockParam param)
            {
                _param = param;
            }

            protected override void ExecuteOnServer()
            {
                var cable = MasterHolder.ItemMaster.GetItemId(_param.CableItemGuid);
                var perLaunch = MasterHolder.ItemMaster.Items.OrbitalPayloads.Single(p => p.PayloadItemGuid == _param.CableItemGuid).CountPerLaunch;
                var pool = ServerContext.GetService<SpaceElevatorCablePool>();
                // 実到着サービスで必要本数を補充する
                // Supply the required cable through the actual arrival service
                while (pool.Amount < _param.CablesPerElevator)
                    ServerContext.GetService<IOrbitalArrivalService>().Arrive(cable, perLaunch);
                Amount = pool.Amount;
            }
        }
    }
}
