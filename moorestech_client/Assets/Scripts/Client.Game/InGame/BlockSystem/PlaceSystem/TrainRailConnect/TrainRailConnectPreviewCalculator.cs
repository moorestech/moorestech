
namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// レール橋脚同士が接続する際、レール同士がどのような接続をするかを計算します
    /// </summary>
    public class TrainRailConnectPreviewCalculator
    {
        public static TrainRailConnectPreviewData CalculatePreviewData(IRailComponentConnectAreaCollider fromArea, IRailComponentConnectAreaCollider toArea)
        {
            // TODO
            
            // 仮実装: 常に前面同士を接続する
            return new TrainRailConnectPreviewData
            {
                IsFromFront = fromArea.IsFront,
                IsToFront = toArea.IsFront,
            };
        }
    }
    
    public struct TrainRailConnectPreviewData
    {
        public bool IsFromFront;
        public bool IsToFront;
    }
}