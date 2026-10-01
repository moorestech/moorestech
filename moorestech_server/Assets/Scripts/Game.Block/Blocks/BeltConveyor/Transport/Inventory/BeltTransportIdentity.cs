using System;
using Core.Item.Interface;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    internal static class BeltTransportIdentity
    {
        internal static Guid ToGuid(ItemInstanceId id)
        {
            var bytes = new byte[16];
            Array.Copy(BitConverter.GetBytes(id.AsPrimitive()), bytes, 8);
            return new Guid(bytes);
        }
        internal static ItemInstanceId ToItemInstanceId(Guid id) => new ItemInstanceId(BitConverter.ToInt64(id.ToByteArray(), 0));
    }
}
