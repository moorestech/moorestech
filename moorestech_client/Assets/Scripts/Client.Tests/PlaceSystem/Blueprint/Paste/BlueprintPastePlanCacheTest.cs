using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste.Cache;
using Client.Game.InGame.Construction;
using Core.Master;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using UniRx;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Blueprint.Paste
{
    public class BlueprintPastePlanCacheTest
    {
        private readonly Guid _blueprint = Guid.NewGuid();

        [Test]
        public void UnchangedInputPlansOnceAcrossFrames()
        {
            var cache = new BlueprintPastePlanCache();
            var calls = 0;
            var first = GetOrPlan(CreateKey(new TestInput()), cache, ref calls);
            var second = GetOrPlan(CreateKey(new TestInput()), cache, ref calls);
            Assert.That(second, Is.SameAs(first));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void EveryPlannerInputChangeInvalidatesThePlan()
        {
            AssertChanged(CreateKey(new TestInput { BlueprintRevision = 2 }));
            AssertChanged(CreateKey(new TestInput { Rotation = 1 }));
            AssertChanged(CreateKey(new TestInput { OccupancyRevision = 1 }));
            AssertChanged(CreateKey(new TestInput { WalletRevision = 1 }));
            AssertChanged(CreateKey(new TestInput { UnlockRevision = 1 }));
            AssertChanged(CreateKey(new TestInput { PaymentWaived = true }));
            AssertChanged(CreateKey(new TestInput { Held = new Dictionary<ItemId, int> { [new ItemId(1)] = 2 } }));
            AssertChanged(CreateKey(new TestInput { Origins = new List<BlueprintPasteOrigin>
            {
                new(new Vector3Int(1, 0, 0), true), new(new Vector3Int(2, 0, 0), false),
            } }));
            AssertChanged(CreateKey(new TestInput { Origins = new List<BlueprintPasteOrigin>
            {
                new(new Vector3Int(1, 0, 0), true), new(new Vector3Int(2, 1, 0), true),
            } }));
            AssertChanged(new BlueprintPastePlanKey(Guid.NewGuid(), 1, 0, 0, 0, 0, false,
                DefaultOrigins(), DefaultHeld()));
        }

        [Test]
        public void MutableInputReferencesCannotChangeStoredKey()
        {
            var cache = new BlueprintPastePlanCache();
            var origins = DefaultOrigins();
            var held = DefaultHeld();
            var calls = 0;
            GetOrPlan(CreateKey(new TestInput { Origins = origins, Held = held }), cache, ref calls);
            origins[1] = new BlueprintPasteOrigin(new Vector3Int(2, 0, 0), false);
            held[new ItemId(1)] = 2;
            GetOrPlan(CreateKey(new TestInput { Origins = origins, Held = held }), cache, ref calls);
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void WalletInitialAndEventUpdatesNotifyCacheOwner()
        {
            var wallet = new ClientRemainingPlacementCountDatastore();
            var notifications = 0;
            wallet.OnWalletChanged.Subscribe(_ => notifications++);
            wallet.ApplyAll(new Dictionary<BlockId, int> { [new BlockId(1)] = 2 });
            wallet.Apply(new BlockId(1), 1);
            Assert.That(notifications, Is.EqualTo(2));
        }

        private void AssertChanged(BlueprintPastePlanKey changed)
        {
            var cache = new BlueprintPastePlanCache();
            var calls = 0;
            GetOrPlan(CreateKey(new TestInput()), cache, ref calls);
            GetOrPlan(changed, cache, ref calls);
            Assert.That(calls, Is.EqualTo(2));
        }

        private BlueprintPastePlanKey CreateKey(TestInput input)
        {
            return new BlueprintPastePlanKey(_blueprint, input.BlueprintRevision, input.Rotation,
                input.OccupancyRevision, input.WalletRevision, input.UnlockRevision, input.PaymentWaived,
                input.Origins, input.Held);
        }

        private sealed class TestInput
        {
            internal ulong BlueprintRevision = 1;
            internal int Rotation;
            internal ulong OccupancyRevision;
            internal ulong WalletRevision;
            internal ulong UnlockRevision;
            internal bool PaymentWaived;
            internal List<BlueprintPasteOrigin> Origins = DefaultOrigins();
            internal Dictionary<ItemId, int> Held = DefaultHeld();
        }

        private static List<BlueprintPasteOrigin> DefaultOrigins() => new()
        {
            new BlueprintPasteOrigin(new Vector3Int(1, 0, 0), true),
            new BlueprintPasteOrigin(new Vector3Int(2, 0, 0), true),
        };

        private static Dictionary<ItemId, int> DefaultHeld() => new() { [new ItemId(1)] = 1 };

        private static BlueprintPastePlan GetOrPlan(BlueprintPastePlanKey key, BlueprintPastePlanCache cache,
            ref int calls)
        {
            if (cache.TryGet(key, out var plan)) return plan;
            calls++;
            plan = new BlueprintPastePlan(Array.Empty<BlueprintPasteCopyPlan>(), false,
                Array.Empty<(ItemId itemId, int held, int required)>());
            cache.Store(key, plan);
            return plan;
        }
    }
}
