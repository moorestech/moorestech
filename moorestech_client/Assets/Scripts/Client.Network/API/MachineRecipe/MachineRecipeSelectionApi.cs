using System.Threading;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;

namespace Client.Network.API.MachineRecipe
{
    public class MachineRecipeSelectionApi
    {
        private readonly PacketExchangeManager _packetExchangeManager;

        public MachineRecipeSelectionApi(PacketExchangeManager packetExchangeManager)
        {
            _packetExchangeManager = packetExchangeManager;
        }

        public async UniTask<MachineRecipeSelectionProtocol.MachineRecipeSelectionResponse> Send(
            MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest request,
            CancellationToken cancellationToken)
        {
            return await _packetExchangeManager.GetPacketResponse<MachineRecipeSelectionProtocol.MachineRecipeSelectionResponse>(request, cancellationToken);
        }
    }
}
