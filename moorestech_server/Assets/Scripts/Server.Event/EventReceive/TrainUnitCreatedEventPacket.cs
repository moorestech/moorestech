namespace Server.Event.EventReceive
{
    // 新規列車生成イベントのタグ定義
    // Event tag for newly created train units
    public sealed class TrainUnitCreatedEventPacket
    {
        public const string EventTag = "va:event:trainUnitCreated";
    }
}
