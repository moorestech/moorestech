using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using Game.Construction;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Game.Blueprint.Planning
{
    internal sealed class BlueprintPastePlannerTestContext
    {
        internal static readonly Guid WireGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        internal static readonly Guid ChainGuid = Guid.Parse("c0000000-0000-0000-0000-000000000003");
        internal readonly TestWorld World;
        internal readonly ConstructionWalletQuery Wallet;

        internal BlueprintPastePlannerTestContext(bool paymentWaived, int remaining)
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            World = new TestWorld(paymentWaived);
            Wallet = new ConstructionWalletQuery(new RemainingReader(remaining));
        }

        internal BlueprintPastePlan Plan(BlueprintJsonObject blueprint, int copyCount, IReadOnlyDictionary<ItemId, int> held)
        {
            var origins = Enumerable.Range(0, copyCount)
                .Select(i => new BlueprintPasteOrigin(new Vector3Int(i * 10, 0, 0), true)).ToArray();
            return BlueprintPastePlanner.Plan(blueprint, origins, 0, World, Wallet, held);
        }

        internal static BlueprintJsonObject Create(params BlockId[] blockIds)
        {
            var blocks = blockIds.Select((id, i) => new BlueprintBlockJsonObject(new Vector3Int(i * 2, 0, 0),
                MasterHolder.BlockMaster.GetBlockMaster(id).BlockGuid.ToString(), (int)BlockDirection.North,
                new Dictionary<string, string>())).ToList();
            return new BlueprintJsonObject("planner", blocks, new List<BlueprintLineJsonObject>(),
                new List<BlueprintLineJsonObject>(), Guid.NewGuid());
        }

        internal static Dictionary<ItemId, int> BlockCosts(BlockId blockId, int cells)
        {
            return ConstructionCostItems.ToItemCounts(MasterHolder.BlockMaster.GetBlockMaster(blockId).RequiredItems)
                .GroupBy(cost => cost.itemId).ToDictionary(group => group.Key, group => group.Sum(cost => cost.count) * cells);
        }

        internal sealed class TestWorld : IBlueprintPasteWorld
        {
            internal readonly HashSet<Vector3Int> Overlaps = new();
            internal readonly HashSet<Guid> Locked = new();
            public bool IsPaymentWaived { get; }

            internal TestWorld(bool paymentWaived)
            {
                IsPaymentWaived = paymentWaived;
            }

            public bool IsOverlapping(BlockPositionInfo positionInfo)
            {
                return Overlaps.Contains(positionInfo.OriginalPos);
            }

            public bool IsBlockUnlocked(Guid blockGuid)
            {
                return !Locked.Contains(blockGuid);
            }

            public bool IsConnectToolUnlocked(Guid connectToolGuid)
            {
                return !Locked.Contains(connectToolGuid);
            }
        }

        private sealed class RemainingReader : IRemainingPlacementCountReader
        {
            private readonly int _remaining;
            public IObservable<Unit> OnWalletChanged => Observable.Never<Unit>();

            internal RemainingReader(int remaining)
            {
                _remaining = remaining;
            }

            public int GetRemainingCount(BlockId blockId)
            {
                return _remaining;
            }
        }
    }
}
