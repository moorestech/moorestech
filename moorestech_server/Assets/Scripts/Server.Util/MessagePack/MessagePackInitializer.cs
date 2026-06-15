using MessagePack;
using MessagePack.Resolvers;

namespace Server.Util.MessagePack
{
    public static class MessagePackInitializer
    {
        public static void Initialize()
        {
            // MessagePackは通信専用。永続化はJsonObject形式で行うため、通信向けの標準リゾルバのみを使う
            // MessagePack is communication-only; persistence uses JsonObject form, so only the standard (network) resolver is registered
            MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard.WithResolver(StandardResolver.Instance);
        }
    }
}
