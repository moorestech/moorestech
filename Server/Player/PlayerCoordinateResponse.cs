using industrialization.OverallManagement.DataStore;

namespace industrialization.Server.Player
{
    public class PlayerCoordinateResponse
    {
        public Coordinate originPosition { get; }
        public int[,] blocks { get; }
        public PlayerCoordinateResponse(Coordinate originPosition)
        {
            this.originPosition = originPosition;
            //TODO ここに座標取得処理を作成
        }
    }
}