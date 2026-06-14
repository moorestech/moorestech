namespace Game.Block.Interface.Component
{
    // 部屋の効果を受け取るブロック側コンポーネント。CleanRoomDatastore が毎tickセットする。
    // Block-side receiver; CleanRoomDatastore pushes the room effect each tick.
    public interface ICleanRoomStateReceiver : IBlockComponent
    {
        void SetCleanRoomEffect(CleanRoomEffect effect);
    }

    // プッシュされる最小ペイロード（算出済みの効果値。プリミティブのみ＝Game.Block はクリーンルーム型を知らない）
    // Minimal pushed payload (already-resolved primitive effect values)
    public readonly struct CleanRoomEffect
    {
        public readonly bool InValidRoom;   // 有効な部屋内にあるか（Invalid/部屋外なら false）
        public readonly int MaxGrade;       // 最大チップグレード天井（0 = Out 相当・出力なし）
        public readonly double DownBinRate; // 汚れ由来の格下げ率

        public CleanRoomEffect(bool inValidRoom, int maxGrade, double downBinRate)
        {
            InValidRoom = inValidRoom;
            MaxGrade = maxGrade;
            DownBinRate = downBinRate;
        }
    }
}
