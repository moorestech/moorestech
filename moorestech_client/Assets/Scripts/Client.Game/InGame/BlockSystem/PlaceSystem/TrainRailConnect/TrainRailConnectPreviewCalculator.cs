using Client.Game.InGame.Block;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// レール橋脚同士が接続する際、レール同士がどのような接続をするかを計算します
    /// </summary>
    public class TrainRailConnectPreviewCalculator
    {
        public static TrainRailConnectPreviewData CalculatePreviewData(BlockGameObject fromBlock, TrainRailConnectAreaCollider toArea)
        {
            // TODO
            
            // 仮実装: 常に前面同士を接続する
            return new TrainRailConnectPreviewData
            {
                FromBlock = fromBlock,
                IsFromFront = true,
                ToBlock = toArea.BlockGameObject,
                IsToFront = true
            };
        }
    }
    
    public struct TrainRailConnectPreviewData
    {
        public BlockGameObject FromBlock;
        public bool IsFromFront;
        
        public BlockGameObject ToBlock;
        public bool IsToFront;
    }
}