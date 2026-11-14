using System.Collections.Generic;
using Core.Update;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.BugReport.GameUiExceptions
{
    /// <summary>
    /// ログを受けた時点で UI（Client.Ui・Client.Ui.State）の例外をセッション通算で数える。2000 件で古い順に捨てる UnityLogRing から数えると、序盤の例外が押し出されて 0 件に見えるため（A-244）
    /// Counts UI (Client.Ui, Client.Ui.State) exceptions for the whole session as each log arrives; counting from UnityLogRing, which drops the oldest past 2000, would push early exceptions out and read as zero (A-244)
    /// </summary>
    public sealed class GameUiExceptionTally : IInitializable
    {
        public const int SampleLimit = 5;

        // スタックの行に載る UI の名前空間の頭（Client.Ui. は Client.Ui.State. も含む）
        // The UI namespace prefix found on stack lines (Client.Ui. also covers Client.Ui.State.)
        private const string UiNamespaceMarker = "Client.Ui.";

        // どのスレッドのログも受けるためロックで守る（前例 UnityLogRing）
        // Locked because logs arrive from any thread (precedent UnityLogRing)
        private readonly object _lock = new();
        private readonly List<GameUiExceptionSample> _samples = new(SampleLimit);
        private int _count;

        public void Initialize()
        {
            Application.logMessageReceivedThreaded += OnLogReceived;
        }

        public void Add(LogType type, string message, string stackTrace)
        {
            // 例外の種別で、スタックに UI のフレームが載るものだけ（UI の購読・コールバックが止まった印）
            // Only exceptions whose stack carries a UI frame (the sign that a UI subscription or callback broke)
            if (type != LogType.Exception || !TryFindFirstUiFrame(stackTrace, out var frame)) return;
            lock (_lock)
            {
                _count++;
                if (_samples.Count < SampleLimit) _samples.Add(new GameUiExceptionSample(GameUpdater.CurrentTick, message, frame));
            }
        }

        // 確保の瞬間の写し。件数はセッション通算、見本は先頭 5 件
        // The capture-moment copy; the count covers the whole session and the samples are the first five
        public GameUiExceptionsRecorded Snapshot()
        {
            lock (_lock)
            {
                return new GameUiExceptionsRecorded(_count, new List<GameUiExceptionSample>(_samples));
            }
        }

        // UI の例外かの唯一の判定。見つかれば最初の UI のフレームの行を返す。行頭（呼ばれたメソッドの型）だけを見て、引数の型に UI の型が載る行を数えない
        // The single test for a UI exception; when found, returns the first UI frame line. Only the line head (the called method's type) counts, so a line merely taking a UI type as an argument does not
        public static bool TryFindFirstUiFrame(string stackTrace, out string frame)
        {
            foreach (var line in stackTrace.Split('\n'))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith(UiNamespaceMarker)) continue;
                frame = trimmed;
                return true;
            }
            frame = null;
            return false;
        }

        private void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            Add(type, condition, stackTrace);
        }
    }
}
