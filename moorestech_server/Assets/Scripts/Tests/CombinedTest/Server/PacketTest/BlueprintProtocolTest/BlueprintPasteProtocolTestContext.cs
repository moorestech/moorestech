using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.Debug;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using Game.Construction;
using Game.Context;
using Game.Entity.Interface;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Event.Notification;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Server.Protocol.PacketResponse.Util.Construction;
using Tests.CombinedTest.Server.PacketTest.Event;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    internal sealed class BlueprintPasteProtocolTestContext : IDisposable
    {
        internal const int PlayerId = PlaceBlockProtocolTestSupport.PlayerId;
        internal static readonly Guid WireGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        internal static readonly Guid ChainGuid = Guid.Parse("c0000000-0000-0000-0000-000000000003");
        internal static readonly Vector3Int Origin = new(10, 0, 10);
        internal readonly ServiceProvider Services;
        internal readonly IOpenableInventory Inventory;
        private readonly PacketResponseCreator _packet;
        private readonly CapturedEventSink _sink;
        private readonly string _debugPath;
        private readonly string _previousDebugPath;

        internal BlueprintPasteProtocolTestContext(bool unlockBlueprint, bool freePlacement)
        {
            // 開発者のデバッグ設定とテストごとに隔離する
            // Isolate each test from persistent developer debug settings
            _previousDebugPath = DebugParametersCacheDirectory.GetOverride();
            _debugPath = Path.Combine(Path.GetTempPath(), "blueprint-paste-" + Guid.NewGuid());
            DebugParametersCacheDirectory.SetOverride(_debugPath);
            DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, freePlacement);
            (_packet, Services) = PlaceBlockProtocolTestSupport.CreateServer();
            Inventory = PlaceBlockProtocolTestSupport.GetInventory(Services);
            var entities = Services.GetRequiredService<IEntitiesDatastore>();
            var playerId = new EntityInstanceId(PlayerId);
            if (!entities.Exists(playerId))
                entities.Add(Services.GetRequiredService<IEntityFactory>().CreateEntity(VanillaEntityType.VanillaPlayer, playerId, Vector3.zero));
            if (unlockBlueprint) Services.GetRequiredService<IGameUnlockStateDataController>().UnlockBlueprint();
            _sink = EventTestUtil.RegisterCaptureSink(Services, PlayerId);
        }

        internal BlueprintJsonObject Create(params BlockId[] blockIds)
        {
            var blocks = blockIds.Select((id, index) => new BlueprintBlockJsonObject(new Vector3Int(index * 3, 0, 0),
                MasterHolder.BlockMaster.GetBlockMaster(id).BlockGuid.ToString(), (int)BlockDirection.North,
                new Dictionary<string, string>())).ToList();
            foreach (var id in blockIds) PlaceBlockProtocolTestSupport.UnlockBlock(Services, id);
            return new BlueprintJsonObject("paste", blocks, new List<BlueprintLineJsonObject>(),
                new List<BlueprintLineJsonObject>(), Guid.NewGuid());
        }

        internal void Register(BlueprintJsonObject blueprint)
        {
            Services.GetRequiredService<IBlueprintDatastore>().Register(blueprint);
        }

        internal void Supply(BlueprintJsonObject blueprint, int copies)
        {
            // 本番と同じ財布・配線距離の費用を用意する
            // Supply costs using the production wallet and line distance rules
            var wallet = Services.GetRequiredService<ConstructionWalletService>().GetQuery(PlayerId);
            var drafts = Enumerable.Range(0, copies).Select(_ => BlueprintPasteCopyBuilder.BuildUnobstructed(blueprint)).ToArray();
            foreach (var (itemId, count) in BlueprintPasteCostCalculator.CalcRequiredItems(drafts, wallet, false))
                Inventory.InsertItem(itemId, count);
        }

        internal void UnlockLines()
        {
            var unlock = Services.GetRequiredService<IGameUnlockStateDataController>();
            unlock.UnlockConnectTool(WireGuid);
            unlock.UnlockConnectTool(ChainGuid);
        }

        internal void Paste(BlueprintJsonObject blueprint, int rotation, params Vector3Int[] origins)
        {
            Send(BlueprintRequest.CreatePasteRequest(blueprint.BlueprintGuid, rotation, origins.ToList()));
        }

        internal void Send(BlueprintRequest request)
        {
            SendForResponse(request);
        }

        internal BlueprintResponse SendForResponse(BlueprintRequest request)
        {
            var packets = _packet.GetPacketResponse(MessagePackSerializer.Serialize(request),
                Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
            Assert.AreEqual(1, packets.Count);
            return MessagePackSerializer.Deserialize<BlueprintResponse>(packets[0]);
        }

        internal void AssertDeniedCount(int count)
        {
            Assert.AreEqual(count, EnumerateBlueprintDenials().Count());
        }

        internal void AssertDenied(BlueprintFailureReason reason, int count)
        {
            var message = EnumerateBlueprintDenials()
                .Single(e => e.MessageId == $"denied.blueprint.{reason}");
            if (0 <= count) Assert.AreEqual(count.ToString(), message.MessageParams[0]);
        }

        private IEnumerable<NotificationMessagePack> EnumerateBlueprintDenials()
        {
            // 同じ通知経路の解除通知を除く
            // Exclude unlock notifications sharing the same event channel.
            return _sink.Events.Where(e => e.Tag == NotificationService.EventTag)
                .Select(e => MessagePackSerializer.Deserialize<NotificationMessagePack>(e.Payload))
                .Where(e => e.Category == NotificationCategory.OperationDenied &&
                    e.MessageId.StartsWith("denied.blueprint.", StringComparison.Ordinal));
        }

        internal static IBlock Place(BlockId blockId, Vector3Int position)
        {
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(blockId, position, BlockDirection.North,
                Array.Empty<BlockCreateParam>(), out var block));
            return block;
        }

        public void Dispose()
        {
            DebugParameters.RemoveBool(DebugParameterKeys.FreeBlockPlacement);
            DebugParametersCacheDirectory.SetOverride(_previousDebugPath);
            if (Directory.Exists(_debugPath)) Directory.Delete(_debugPath, true);
        }
    }
}
