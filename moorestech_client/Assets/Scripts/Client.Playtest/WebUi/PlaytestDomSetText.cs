using Client.Playtest.UiElements;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using System;
using Client.WebUiHost.Boot.Transport;
using Client.WebUiHost.Cef;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Client.Playtest.WebUi
{
    internal class DomSetTextResult
    {
        public UiSetTextStatus Status;
        public int MatchCount;
        public string ActualValue;
    }

    /// <summary>
    ///     Web UIのテキスト欄へ値を設定させ、結果を受け取る
    ///     Asks the Web UI to set a text field's value and receives the outcome
    /// </summary>
    internal class PlaytestDomSetText
    {
        private const string SetTextTopic = "playtest.dom_set_text";
        private readonly PlaytestWebUiRequestChannel<DomSetTextResult> _channel = new();
        public readonly IConnectionActionHandler ResultHandler;

        public PlaytestDomSetText()
        {
            ResultHandler = new DomSetTextResultActionHandler(_channel);
        }

        public async UniTask<WebUiChannelResponse<DomSetTextResult>> Request(WebSocketHub hub, string testid, string text, float timeoutSeconds)
        {
            if (!WebUiCefConnection.TryGetConnection(hub, out var connectionId))
            {
                Debug.LogWarning("[PlaytestUi] No registered CEF connection");
                return new WebUiChannelResponse<DomSetTextResult>(WebUiResponseState.Stopped, null, "No registered CEF connection");
            }
            if (!(0f < timeoutSeconds)) throw new TimeoutException("UI request deadline expired before delivery");
            var requestId = _channel.Open(connectionId, hub);
            if (!hub.PublishTo(connectionId, SetTextTopic, WebUiJson.Serialize(new DomSetTextRequest { RequestId = requestId, Testid = testid, Text = text })))
            {
                _channel.Stop(requestId);
                return new WebUiChannelResponse<DomSetTextResult>(WebUiResponseState.Stopped, null, "CEF disconnected or not subscribed");
            }
            return await _channel.Await(requestId, timeoutSeconds);
        }

        public void Reset()
        {
            _channel.Reset();
        }

        private class DomSetTextRequest
        {
            public string RequestId;
            public string Testid;
            public string Text;
        }
    }
}
