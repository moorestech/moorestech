namespace Client.Game.InGame.Tutorial
{
    public class WorldPinPresentationData
    {
        public int Revision;
        public WorldPinData[] Pins;
    }

    public class WorldPinData
    {
        public string PinId;
        public string Text;

        // 正規化ビューポート座標（0..1、左上原点）。OnScreen時のみ有効
        // Normalized viewport position (0..1, top-left origin); valid only while OnScreen
        public float ScreenX;
        public float ScreenY;
        public bool OnScreen;

        // 画面中心からターゲットへのスクリーン空間方向（CSS座標系、下が正）。画面外矢印用
        // Screen-space direction from screen center toward the target (CSS axes, +Y down) for off-screen arrows
        public float DirectionX;
        public float DirectionY;
    }
}
