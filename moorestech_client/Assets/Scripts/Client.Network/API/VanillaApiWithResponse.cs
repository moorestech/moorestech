using System.Threading;
using Client.Network.API.Identity;
using Client.Network.API.Requests;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Game.Context;

namespace Client.Network.API
{
    public class VanillaApiWithResponse
    {
        private readonly PacketExchangeManager _packetExchangeManager;
        private readonly InitialHandshakeClient _initialHandshakeClient;

        public WorldQueryApi World { get; }
        public ProgressionQueryApi Progression { get; }
        public TrainRequestApi Train { get; }
        public BlockRequestApi Block { get; }
        public InventoryQueryApi Inventory { get; }

        public VanillaApiWithResponse(PacketExchangeManager packetExchangeManager)
        {
            _packetExchangeManager = packetExchangeManager;
            _initialHandshakeClient = new InitialHandshakeClient(ServerContext.GetService<IItemStackLevelUnlocker>());
            World = new WorldQueryApi(packetExchangeManager);
            Progression = new ProgressionQueryApi(packetExchangeManager);
            Train = new TrainRequestApi(packetExchangeManager);
            Block = new BlockRequestApi(packetExchangeManager);
            Inventory = new InventoryQueryApi(packetExchangeManager, ServerContext.ItemStackFactory);
        }

        public UniTask<InitialHandshakeAttempt> InitialHandShake(string playerIdentity, CancellationToken ct)
        {
            return _initialHandshakeClient.RunAsync(this, _packetExchangeManager, playerIdentity, ct);
        }
    }
}
