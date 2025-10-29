namespace Game.Block.Interface
{
    public struct BlockCreateParam
    {
        public readonly string Key;
        public readonly byte[] Value;
        public BlockCreateParam(string key, byte[] value)
        {
            Key = key;
            Value = value;
        }
    }
}