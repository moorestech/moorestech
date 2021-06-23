using System.Collections.Generic;
using industrialization.Server.Util.ObjectToByteArray.Implementation;

namespace industrialization.Server.Util.ObjectToByteArray
{
    public static class SendObjectToByteArray
    {
        public static byte[] Convert(List<ISendObject> sendObjects)
        {
            var bytes = new List<byte>();
            foreach (var _byte in sendObjects)
            {
                bytes.AddRange(_byte.GetByteArray());
            }

            return bytes.ToArray();
        }
    }
}