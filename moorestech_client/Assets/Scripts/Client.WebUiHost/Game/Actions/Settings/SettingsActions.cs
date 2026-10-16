using Client.Input;
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    public static class SettingsActions
    {
        public static void Register(WebSocketHub hub)
        {
            hub.RegisterAction(new SetSprintInputModeActionHandler());
        }
    }

    /// <summary>
    /// Webからの走り方式の変更を保存窓口へ渡す
    /// Passes a sprint-mode change from the web to the preference store
    /// </summary>
    public class SetSprintInputModeActionHandler : IActionHandler
    {
        public string ActionType => "settings.setSprintInputMode";

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var wire = payload?["mode"]?.ToString();

            // 不明値は失敗契約で返し、保存値は変えない
            // An unknown value returns the failure contract and leaves the saved value as is
            if (!SprintInputModeWire.TryFromWire(wire, out var mode))
            {
                Debug.LogWarning($"[SettingsActions] Rejected unknown sprint mode: {wire}");
                return UniTask.FromResult(ActionResult.Fail("unknown_sprint_mode"));
            }
            SprintInputModePreference.SetMode(mode);
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    internal static class SprintInputModeWire
    {
        public static string ToWire(SprintInputMode mode)
        {
            return mode == SprintInputMode.Toggle ? "toggle" : "hold";
        }

        public static bool TryFromWire(string wire, out SprintInputMode mode)
        {
            mode = SprintInputMode.Toggle;
            if (wire == "toggle") return true;
            mode = SprintInputMode.Hold;
            return wire == "hold";
        }
    }
}
