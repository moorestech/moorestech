using System;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.UitkStage0Probe
{
    // S15: Webの document 全体の pointer-events を切り替える要求を送り、Webからの適用報告を受ける
    // S15: sends requests that toggle pointer-events on the whole Web document and receives the Web's applied report
    public static class PocPointerEventsDriver
    {
        private const string ProbeTopic = "poc.pointer_events";
        private static readonly AckHandler Handler = new();
        private static bool _registered;

        public static string LastAck { get; private set; } = "";

        public static void Request(string mode)
        {
            var hub = global::Client.WebUiHost.Boot.WebUiHost.Hub;
            if (!_registered)
            {
                hub.RegisterAction(Handler);
                _registered = true;
            }
            var payload = new JObject { ["requestId"] = Guid.NewGuid().ToString(), ["mode"] = mode };
            hub.Publish(ProbeTopic, payload.ToString(Newtonsoft.Json.Formatting.None));
            Debug.Log($"[PocPointerEvents] request mode={mode} frame={Time.frameCount}");
        }

        private class AckHandler : IActionHandler
        {
            public string ActionType => "poc.pointer_events_ack";

            public UniTask<ActionResult> ExecuteAsync(JObject payload)
            {
                LastAck = payload?.ToString(Newtonsoft.Json.Formatting.None) ?? "";
                Debug.Log($"[PocPointerEvents] ack {LastAck} frame={Time.frameCount}");
                return UniTask.FromResult(ActionResult.Success());
            }
        }
    }
}
