using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Server.Event
{
    public static class ListDeepCopy
    {
        public static T Copy<T>(this T src)
        {
            using (var memoryStream = new MemoryStream())
            {
                var binaryFormatter
                    = new BinaryFormatter();
                binaryFormatter.Serialize(memoryStream, src); // シリアライズ
                memoryStream.Seek(0, SeekOrigin.Begin);
                return (T)binaryFormatter.Deserialize(memoryStream); // デシリアライズ
            }
        }
    }
}