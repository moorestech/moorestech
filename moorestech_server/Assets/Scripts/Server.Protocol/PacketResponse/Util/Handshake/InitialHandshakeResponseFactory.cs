using System.Linq;
using Core.Item.Interface;
using Game.Block.Interface.MachineUpgrade;
using Game.Construction;
using Game.Entity.Interface;
using Game.Hotbar;
using Game.PlayerRiding.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;
using Server.Event.EventReceive.MachineUpgrade;
using Server.Protocol.PacketResponse.Handshake;
using Server.Util.MessagePack;
using static Server.Event.EventReceive.ItemStackLevelUnlockEventPacket;

namespace Server.Protocol.PacketResponse.Util.Handshake
{
    internal sealed class InitialHandshakeResponseFactory
    {

        private readonly IEntitiesDatastore _entitiesDatastore;
        private readonly IEntityFactory _entityFactory;
        private readonly IWorldSettingsDatastore _worldSettingsDatastore;
        private readonly IPlayerRidingDatastore _playerRidingDatastore;
        private readonly IItemStackLevelLookup _itemStackLevelLookup;
        private readonly IHotbarAssignmentLookup _hotbarAssignmentLookup;
        private readonly IRemainingPlacementCountLookup _remainingPlacementCountLookup;
        private readonly IModuleSlotStackLimitLookup _moduleSlotStackLimitLookup;
        private readonly ILevelInheritanceCraftCapLookup _levelInheritanceCraftCapLookup;

        internal InitialHandshakeResponseFactory(ServiceProvider serviceProvider)
        {
            _itemStackLevelLookup = serviceProvider.GetService<IItemStackLevelLookup>();
            _entitiesDatastore = serviceProvider.GetService<IEntitiesDatastore>();
            _entityFactory = serviceProvider.GetService<IEntityFactory>();
            _worldSettingsDatastore = serviceProvider.GetService<IWorldSettingsDatastore>();
            _playerRidingDatastore = serviceProvider.GetService<IPlayerRidingDatastore>();
            _hotbarAssignmentLookup = serviceProvider.GetService<IHotbarAssignmentLookup>();
            _remainingPlacementCountLookup = serviceProvider.GetService<IRemainingPlacementCountLookup>();
            _moduleSlotStackLimitLookup = serviceProvider.GetService<IModuleSlotStackLimitLookup>();
            _levelInheritanceCraftCapLookup = serviceProvider.GetService<ILevelInheritanceCraftCapLookup>();
        }

        internal InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack CreateResponse(int playerId)
        {
            // 乗り物に乗っているかどうかの状態の取得
            // Get the riding state if the player is currently riding something.
            RidableIdentifierMessagePack ridingTarget = null;
            var ridingSeatIndex = -1;
            if (_playerRidingDatastore.EvaluateOnLogin(playerId)
                && _playerRidingDatastore.TryGetRidingState(playerId, out var state))
            {
                ridingTarget = state.Identifier.ToMessagePack();
                ridingSeatIndex = state.SeatIndex;
            }

            var playerPos = GetPlayerPosition(new EntityInstanceId(playerId));

            // 解放済みスタックレベルを初期データとして同梱する
            // Bundle unlocked stack levels as part of the initial data
            var itemStackLevels = _itemStackLevelLookup.UnlockedLevels
                .Select(level => new ItemStackLevelMessagePack(level.Key, level.Value))
                .ToArray();

            // ホットバー割当も初期データとして同梱し、ログイン後の追加往復とnull経路をなくす
            // Bundle the hotbar assignments too, removing the extra post-login round trip and its null path
            var hotbarAssignments = _hotbarAssignmentLookup.GetAssignments(playerId).ToArray();

            // 残り設置数も初期データとして同梱し、ログイン直後からプレビュー・表示に使えるようにする
            // Bundle remaining placements as initial data so previews and displays work right after login
            var remainingPlacementCounts = _remainingPlacementCountLookup.GetRemainingCounts(playerId)
                .Select(pair => new RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack(pair.walletBlockId.AsPrimitive(), pair.remainingCount))
                .ToArray();

            // 機械の強化系（スロット上限・製作上限）も初期データとして同梱する
            // Bundle the machine-upgrade state (slot limit, craft cap) as initial data too
            var levelInheritanceCraftCaps = _levelInheritanceCraftCapLookup.UnlockedCaps
                .Select(cap => new LevelInheritanceCraftCapUnlockEventPacket.LevelInheritanceCraftCapMessagePack(cap.Key, cap.Value))
                .ToArray();

            var accepted = new HandshakeAcceptedMessagePack(playerPos, ridingTarget, ridingSeatIndex, itemStackLevels, hotbarAssignments, remainingPlacementCounts,
                _moduleSlotStackLimitLookup.CurrentLevel, levelInheritanceCraftCaps, playerId);
            return new InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack(accepted);

            #region Internal

            Vector3MessagePack GetPlayerPosition(EntityInstanceId entityInstanceId)
            {
                if (_entitiesDatastore.Exists(entityInstanceId))
                {
                    // 保存済みの座標を復元する
                    // Restore the saved position
                    var pos = _entitiesDatastore.GetPosition(entityInstanceId);
                    return new Vector3MessagePack(pos.x, pos.y, pos.z);
                }

                var spawnPoint = _worldSettingsDatastore.WorldSpawnPoint;
                var playerEntity = _entityFactory.CreateEntity(VanillaEntityType.VanillaPlayer, entityInstanceId, spawnPoint);
                _entitiesDatastore.Add(playerEntity);

                // 新規プレイヤーにはスポーン地点を返す
                // Return the spawn point for a new player
                return new Vector3MessagePack(spawnPoint);
            }

            #endregion
        }
    }
}
