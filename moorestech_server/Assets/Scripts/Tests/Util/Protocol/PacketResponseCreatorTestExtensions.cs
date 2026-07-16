using System.Collections.Generic;
using System.Reflection;

namespace Server.Protocol
{
    public static class PacketResponseCreatorTestExtensions
    {
        private static readonly MethodInfo GetResponseCore = typeof(PacketResponseCreator).GetMethod(
            "GetResponseCore",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public static List<byte[]> GetPacketResponseForTest(
            this PacketResponseCreator creator,
            byte[] payload,
            PacketResponseContext context)
        {
            // 既存プロトコル単体テストは非公開の共通処理をリフレクションで直接呼ぶ
            // Existing protocol unit tests invoke the private shared core through reflection
            var arguments = new object[] { payload, context, false, null };
            GetResponseCore.Invoke(creator, arguments);
            return (List<byte[]>)arguments[3];
        }
    }
}
