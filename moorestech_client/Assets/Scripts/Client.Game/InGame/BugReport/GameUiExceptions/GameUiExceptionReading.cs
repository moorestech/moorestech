using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Client.Game.InGame.BugReport.GameUiExceptions
{
    public enum GameUiExceptionStatus
    {
        Recorded,
        Unknown,
    }

    /// <summary>
    /// manifest.gameUiExceptions の契約。数えた箱と数えていない箱を型で分け、数えていない箱は件数も見本も持たない（Web の AppErrorBoundary の代わり。A-244）
    /// The manifest.gameUiExceptions contract; counted and uncounted boxes are separate types and an uncounted box carries neither count nor samples (stands in for the Web AppErrorBoundary; A-244)
    /// </summary>
    public abstract class GameUiExceptionReading
    {
        // JSON の判別子。綴りは webUiHealth と同じく欄の属性で camelCase に固定する（manifest の Settings は enum の変換器を持たない）
        // The JSON discriminator; spelled camelCase by a per-field attribute as in webUiHealth (the manifest Settings carry no enum converter)
        [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
        public readonly GameUiExceptionStatus Status;

        protected GameUiExceptionReading(GameUiExceptionStatus status)
        {
            Status = status;
        }
    }

    // セッション通算で数えた箱。0 件は「数えて無かった」という実値
    // A box counted over the whole session; zero is the real value "counted, none found"
    public sealed class GameUiExceptionsRecorded : GameUiExceptionReading
    {
        public readonly int Count;
        public readonly IReadOnlyList<GameUiExceptionSample> Samples;

        public GameUiExceptionsRecorded(int count, IReadOnlyList<GameUiExceptionSample> samples) : base(GameUiExceptionStatus.Recorded)
        {
            Count = count;
            Samples = samples;
        }
    }

    // 数えていない箱（確保時に数え手の値が無い・前回セッションのクラッシュ）。理由は manifest の missing が持つ
    // An uncounted box (no tally value at capture, previous-session crash); the reason lives in the manifest's missing list
    public sealed class GameUiExceptionsUnknown : GameUiExceptionReading
    {
        public GameUiExceptionsUnknown() : base(GameUiExceptionStatus.Unknown)
        {
        }
    }

    public sealed class GameUiExceptionSample
    {
        public readonly ulong Tick;
        public readonly string Message;
        public readonly string Frame;

        public GameUiExceptionSample(ulong tick, string message, string frame)
        {
            Tick = tick;
            Message = message;
            Frame = frame;
        }
    }
}
