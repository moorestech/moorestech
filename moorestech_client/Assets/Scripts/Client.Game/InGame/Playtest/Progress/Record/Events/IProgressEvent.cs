using Newtonsoft.Json.Linq;

namespace Client.Game.InGame.Playtest.Progress.Record.Events
{
    // 進行記録のイベント1件。種別ごとのクラスが自分の集計への足し方とJSONの形を持ち、集計側に種別の分岐を置かない（D25）
    // One progress event; each type's class owns how it adds to the aggregate and its JSON shape, so the aggregate never branches on type (D25)
    internal interface IProgressEvent
    {
        string T { get; }
        ulong Tick { get; }
        void ApplyTo(ProgressRecordAggregate aggregate);
        JObject ToJson();
    }
}
