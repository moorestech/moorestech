namespace Client.Game.InGame.Train.RailGraph
{
    /// <summary>
    ///     レール描画1本を表すID（canonicalな区間ペア）の符号化・復号。canonical の選択は RailSegmentPairing が正本
    ///     Encode/decode the id of one drawn rail (canonical edge pair); RailSegmentPairing owns the canonical choice
    /// </summary>
    public static class RailObjectIdCodec
    {
        public static ulong ComputeRailObjectId(int canonicalFrom, int canonicalTo)
        {
            return (ulong)canonicalFrom + ((ulong)canonicalTo << 32);
        }

        public static (int canonicalFrom, int canonicalTo) Decode(ulong railObjectId)
        {
            return (unchecked((int)(uint)railObjectId), unchecked((int)(uint)(railObjectId >> 32)));
        }
    }
}
