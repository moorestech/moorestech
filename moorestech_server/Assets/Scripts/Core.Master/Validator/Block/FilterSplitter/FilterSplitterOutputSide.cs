namespace Core.Master.Validator.Block.FilterSplitter
{
    // 入口から入ってまっすぐ進んだ先が正面、その向きを向いて左手が左、右手が右（ユーザー裁定 2026-11-19）
    // Front is straight ahead from the entry; facing that way the left hand is left and the right hand is right (user decision 2026-11-19)
    public enum FilterSplitterOutputSide
    {
        Left = 0,
        Front = 1,
        Right = 2,
    }
}
