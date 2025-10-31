namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public class TrainCarPlaceSystem : IPlaceSystem
    {
        public void Enable()
        {
        }
        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            // TODO マウスカーソルから線路へのレイの当たり判定を取得
            
            // TODO もし線路にあたっていれば、線路に列車を置く用にプレビューを表示
            // TODO レール側に列車を置くプレビュー表示をする。どの座標に置けばいいかの取得メソッドを定義する
            
            // TODO 列車設置プロトコルを送信する
        }
        public void Disable()
        {
        }
    }
}