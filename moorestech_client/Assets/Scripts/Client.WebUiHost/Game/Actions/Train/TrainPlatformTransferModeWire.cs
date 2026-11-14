using Client.Ui.State.Train.Platform;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    // State の移送モードと wire 文字列 "loadToTrain"/"unloadToPlatform" の対応表。サーバーの値との写しは State の TrainPlatformTransferModeConversion が持つ
    // Maps the State transfer mode to the wire strings "loadToTrain"/"unloadToPlatform"; the mapping to server values lives in State's TrainPlatformTransferModeConversion
    internal static class TrainPlatformTransferModeWire
    {
        public static string ToWire(TrainPlatformTransferMode mode)
        {
            return mode == TrainPlatformTransferMode.LoadToTrain ? "loadToTrain" : "unloadToPlatform";
        }

        public static bool TryParse(JToken token, out TrainPlatformTransferMode mode)
        {
            mode = default;
            if (token is not JValue { Type: JTokenType.String } value) return false;
            switch ((string)value)
            {
                case "loadToTrain":
                    mode = TrainPlatformTransferMode.LoadToTrain;
                    return true;
                case "unloadToPlatform":
                    mode = TrainPlatformTransferMode.UnloadToPlatform;
                    return true;
                default:
                    return false;
            }
        }
    }
}
