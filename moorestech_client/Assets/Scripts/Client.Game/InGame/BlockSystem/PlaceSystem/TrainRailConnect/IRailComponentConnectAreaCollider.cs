using Client.Game.InGame.BlockSystem.StateProcessor;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    
    /// <summary>
    /// ここでいうRailComponentはサーバー側の鉄道システムのことを指す
    /// The term "RailComponent" here refers to the railway system on the server side.
    /// </summary>
    public interface IRailComponentConnectAreaCollider : IBlockGameObjectInnerComponent
    {
        public bool IsFront { get; }
        
        public RailComponentSpecifier CreateRailComponentSpecifier();
    }
}