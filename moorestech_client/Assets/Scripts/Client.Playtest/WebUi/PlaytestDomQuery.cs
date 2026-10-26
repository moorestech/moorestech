using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;

namespace Client.Playtest.WebUi
{
    internal class DomQueryResult
    {
        public bool Found;
        public bool Visible;
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public float DevicePixelRatio;
        public bool HitTestPassed;
        public string Text;
        public int MatchCount;
        public string[] StateFlags;
    }

    /// <summary>
    ///     Web UIへtestidのDOM矩形・文字列・状態フラグを問い合わせる
    ///     Queries the Web UI for a testid's DOM rect, text, and state flags
    /// </summary>
    internal class PlaytestDomQuery
    {
        private const string QueryTopic = "playtest.dom_query";
        private readonly PlaytestWebUiRequestChannel<DomQueryResult> _channel = new();
        public readonly IActionHandler ResultHandler;

        public PlaytestDomQuery()
        {
            ResultHandler = new DomQueryResultActionHandler(_channel);
        }

        public async UniTask<WebUiChannelResponse<DomQueryResult>> Query(WebSocketHub hub, string testid, float timeoutSeconds)
        {
            var requestId = _channel.Open();
            hub.Publish(QueryTopic, WebUiJson.Serialize(new DomQueryRequest { RequestId = requestId, Testid = testid }));
            return await _channel.Await(requestId, timeoutSeconds);
        }

        public void Reset()
        {
            _channel.Reset();
        }

        public static bool IsValidResult(DomQueryResult result)
        {
            // 有限性はWebUiPayloadReader.TryReadFloatが見るので、ここでは値域と発見・件数・矩形の相関だけを検証する
            // TryReadFloat already checks finiteness, so validate only ranges and the found/count/rect correlation here
            if (result.Width < 0f || result.Height < 0f || result.DevicePixelRatio <= 0f) return false;
            if (result.Found != (0 < result.MatchCount) || result.StateFlags == null) return false;
            return !result.HitTestPassed || (0f < result.Width && 0f < result.Height);
        }

        private class DomQueryRequest
        {
            public string RequestId;
            public string Testid;
        }
    }
}
