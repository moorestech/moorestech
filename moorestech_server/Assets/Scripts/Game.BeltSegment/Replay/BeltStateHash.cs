using System;
namespace Game.BeltSegment
{
    internal static class BeltStateHash
    {
        internal const uint Initial = 2166136261;
        internal static uint Add(uint hash, int value)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++) { hash = (hash ^ (byte)value) * 16777619; value >>= 8; }
                return hash;
            }
        }
        internal static uint Item(uint hash, in BeltItem item)
        {
            Span<byte> bytes = stackalloc byte[16];
            item.Guid.TryWriteBytes(bytes);
            unchecked { foreach (byte value in bytes) hash = (hash ^ value) * 16777619; }
            return Add(Add(hash, item.ItemId), (int)item.AcceptedInput);
        }
    }
}
