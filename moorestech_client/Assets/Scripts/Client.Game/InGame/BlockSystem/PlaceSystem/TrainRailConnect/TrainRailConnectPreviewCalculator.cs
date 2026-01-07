using Game.Train.RailGraph;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// レール橋脚同士が接続する際、レール同士がどのような接続をするかを計算します
    /// </summary>
    public class TrainRailConnectPreviewCalculator
    {
        public static TrainRailConnectPreviewData CalculatePreviewData(ConnectionDestination from, ConnectionDestination to)
        {
            return new TrainRailConnectPreviewData
            {
                FromDestination = from,
                ToDestination = to,
            };
        }
    }
    
    public struct TrainRailConnectPreviewData
    {
        public ConnectionDestination FromDestination;
        public ConnectionDestination ToDestination;
    }
}