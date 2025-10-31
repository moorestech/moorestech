using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController
{
    public interface IBlockPreviewStateProcessor
    {
        public void SetPreviewStateDetail(PlaceInfo placeInfo);
    }
}