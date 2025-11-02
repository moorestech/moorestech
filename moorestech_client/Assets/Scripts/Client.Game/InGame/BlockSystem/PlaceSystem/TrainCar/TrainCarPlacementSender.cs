using Client.Game.InGame.Context;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public class TrainCarPlacementSender : ITrainCarPlacementSender
    {
        public void Send(RailComponentSpecifier specifier, int hotBarSlot)
        {
            ClientContext.VanillaApi.SendOnly.PlaceTrainOnRail(specifier, hotBarSlot);
        }
    }
}

