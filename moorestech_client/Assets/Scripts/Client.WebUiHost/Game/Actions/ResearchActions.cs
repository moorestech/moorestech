using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.WebUiHost.Game.Topics;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// research.complete: 研究実行をサーバーへ送信し、応答の全ノード状態で topic を再配信する
    /// research.complete: sends a research completion to the server and republishes the topic with the response states
    /// </summary>
    public class ResearchCompleteActionHandler : IActionHandler
    {
        public string ActionType => "research.complete";

        private readonly ResearchTopic _researchTopic;

        public ResearchCompleteActionHandler(ResearchTopic researchTopic)
        {
            _researchTopic = researchTopic;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return ActionResult.Fail("invalid_payload");
            if (payload["researchGuid"] is not JValue { Type: JTokenType.String } guidValue) return ActionResult.Fail("invalid_payload");
            if (!Guid.TryParse((string)guidValue, out var researchGuid)) return ActionResult.Fail("invalid_guid");

            var response = await ClientContext.VanillaApi.Response.CompleteResearch(researchGuid, CancellationToken.None);
            if (response == null) return ActionResult.Fail("research_failed");

            // 成否に関わらず最新全ノード状態を配信し、Web を正しい状態へ収束させる
            // Publish the latest node states regardless of success so the web converges to the true state
            _researchTopic.ApplyNodeStates(response.NodeState.ToDictionary());
            return response.Success ? ActionResult.Success() : ActionResult.Fail("research_failed");
        }
    }
}
