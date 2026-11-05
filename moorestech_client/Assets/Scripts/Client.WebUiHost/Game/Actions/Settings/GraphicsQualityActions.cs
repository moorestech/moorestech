using Client.Ui.State.Settings;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// Webからのプリセット選択を保存窓口へ渡す
    /// Passes a preset selection from the web to the preference store
    /// </summary>
    public class ApplyGraphicsQualityPresetActionHandler : IActionHandler
    {
        private readonly ISettingsCommands _commands;
        public string ActionType => "settings.applyGraphicsQualityPreset";

        public ApplyGraphicsQualityPresetActionHandler(ISettingsCommands commands)
        {
            _commands = commands;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var wire = payload?["preset"]?.ToString();

            // 不明値は失敗契約で返し、保存値は変えない
            // An unknown value returns the failure contract and leaves the saved value as is
            if (!GraphicsQualityWire.TryPresetFromWire(wire, out var preset))
            {
                Debug.LogWarning($"[GraphicsQualityActions] Rejected unknown graphics quality preset: {wire}");
                return UniTask.FromResult(ActionResult.Fail("unknown_graphics_quality_preset"));
            }
            _commands.ApplyGraphicsPreset(preset);
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    /// <summary>
    /// Webからの項目変更（対象と値）を保存窓口へ渡す
    /// Passes an item change (selected item and value) from the web to the preference store
    /// </summary>
    public class SetGraphicsQualityActionHandler : IActionHandler
    {
        private readonly ISettingsCommands _commands;
        public string ActionType => "settings.setGraphicsQuality";

        public SetGraphicsQualityActionHandler(ISettingsCommands commands)
        {
            _commands = commands;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            // 定義外の値を含む組は失敗契約で返し、保存値は変えない
            // A set containing an undefined value returns the failure contract and leaves the saved value as is
            var status = GraphicsQualityWire.Parse(payload, out var settings, out var invalidField);
            if (status != GraphicsQualityParseStatus.Success)
            {
                var reason = status switch
                {
                    GraphicsQualityParseStatus.MissingField => "missing_field",
                    GraphicsQualityParseStatus.InvalidType => "invalid_type",
                    GraphicsQualityParseStatus.UnsupportedValue => "unsupported_value",
                    _ => throw new System.ArgumentOutOfRangeException(nameof(status), status, null),
                };
                var error = $"invalid_graphics_quality:{reason}:{invalidField}";
                Debug.LogWarning($"[GraphicsQualityActions] Rejected invalid graphics quality: {error}");
                return UniTask.FromResult(ActionResult.Fail(error));
            }
            _commands.SetGraphicsQuality(settings);
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
