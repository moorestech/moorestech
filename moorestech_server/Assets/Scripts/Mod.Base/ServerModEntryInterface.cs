using Core.Item.Interface;
using Game.Block.Factory;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.RecipeConfig;
using Game.Crafting.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol;

namespace Mod.Base
{
    public class ServerModEntryInterface
    {
        /// <summary>
        ///     パケットを送信することができるインスタンス
        /// </summary>
        public readonly PacketResponseCreator PacketResponseCreator;

        /// <summary>
        ///     各種サービスを取得できるDIコンテナ
        /// </summary>
        public readonly ServiceProvider ServiceProvider;

        public ServerModEntryInterface(ServiceProvider serviceProvider, PacketResponseCreator packetResponseCreator)
        {
            ServiceProvider = serviceProvider;
            PacketResponseCreator = packetResponseCreator;
        }
    }
}