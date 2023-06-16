using MessagePack;

namespace Server.Protocol.Base
{
    [MessagePackObject(keyAsPropertyName:true)]
    public class ToServerProtocolMessagePackBase
    {
        public string ToServerTag { get; set; }
    }
    [MessagePackObject(keyAsPropertyName:true)]
    public class ToClientProtocolMessagePackBase
    {
        public string ToClientTag { get; set; }
    }
    
    //TODO リクエストとレスポンスでクラスを分ける
    //TODO 送るときにタグをチェックする
}