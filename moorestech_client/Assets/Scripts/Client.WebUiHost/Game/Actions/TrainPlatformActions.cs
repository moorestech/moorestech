using Client.Ui.State.Train.Platform.Edit;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// 開いている列車プラットフォームの転送モードを目標値で設定する。開いているブロックの解決・種別の判定・送信は State の TrainBlockCommands が持つ（DC-23 A）
    /// Sets the open train platform's transfer mode to an explicit target; State's TrainBlockCommands resolves the open block, checks its kind and sends (DC-23 A)
    /// </summary>
    public class TrainPlatformSetTransferModeActionHandler : IActionHandler
    {
        public string ActionType => "train_platform.set_transfer_mode";

        private readonly ITrainBlockCommands _trainBlockCommands;

        public TrainPlatformSetTransferModeActionHandler(ITrainBlockCommands trainBlockCommands)
        {
            _trainBlockCommands = trainBlockCommands;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return ActionResult.Fail("invalid_payload");
            if (!TrainPlatformTransferModeWire.TryParse(payload["mode"], out var mode)) return ActionResult.Fail("invalid_mode");

            // 表示の正本は後から届く StateDetail。失敗コードは State の写像で綴る
            // The later StateDetail stays the view source of truth; failure codes are spelled by the State mapping
            var result = await _trainBlockCommands.SetTransferMode(mode);
            return ActionResult.FromCommandResult(result);
        }
    }
}
