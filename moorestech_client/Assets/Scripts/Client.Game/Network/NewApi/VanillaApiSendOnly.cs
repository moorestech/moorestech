using Core.Item;
using MainGame.Network.Settings;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Network.NewApi
{
    public class VanillaApiSendOnly
    {
        private readonly ServerConnector _serverConnector;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly PlayerConnectionSetting _playerConnectionSetting;
        
        public VanillaApiSendOnly(ServerConnector serverConnector, ItemStackFactory itemStackFactory, PlayerConnectionSetting playerConnectionSetting)
        {
            _serverConnector = serverConnector;
            _itemStackFactory = itemStackFactory;
            _playerConnectionSetting = playerConnectionSetting;
        }
        
        public void SetOpenCloseBlock(int playerId, Vector2Int pos, bool isOpen)
        {
            var request = new BlockInventoryOpenCloseProtocolMessagePack(playerId, pos.x, pos.y, isOpen);
            _serverConnector.Send(request);
        }
    }
}