using MessagePack;

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
    
    public static class BlockCreateParamExtension
    {
        public static TBlockState GetStateDetail<TBlockState>(this BlockCreateParam[] createParams, string stateKey)
        {
            foreach (var param in createParams)
            {
                if (param.Key != stateKey) continue;
                var bytes = param.Value;
                return MessagePackSerializer.Deserialize<TBlockState>(bytes);
            }
            
            return default;
        }
    }
}