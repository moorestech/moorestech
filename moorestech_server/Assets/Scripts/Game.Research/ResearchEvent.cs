using System;
using UniRx;

namespace Game.Research
{
    public class ResearchEvent
    {
        // 研究完了イベント
        private readonly Subject<(int playerId, Guid researchGuid)> _onResearchCompleted = new();
        public IObservable<(int playerId, Guid researchGuid)> OnResearchCompleted => _onResearchCompleted;

        // 研究開始イベント（将来的な拡張用）
        private readonly Subject<(int playerId, Guid researchGuid)> _onResearchStarted = new();
        public IObservable<(int playerId, Guid researchGuid)> OnResearchStarted => _onResearchStarted;

        // 研究失敗イベント
        private readonly Subject<(int playerId, Guid researchGuid, string reason)> _onResearchFailed = new();
        public IObservable<(int playerId, Guid researchGuid, string reason)> OnResearchFailed => _onResearchFailed;

        public void PublishResearchCompleted(int playerId, Guid researchGuid)
        {
            _onResearchCompleted.OnNext((playerId, researchGuid));
        }

        public void PublishResearchStarted(int playerId, Guid researchGuid)
        {
            _onResearchStarted.OnNext((playerId, researchGuid));
        }

        public void PublishResearchFailed(int playerId, Guid researchGuid, string reason)
        {
            _onResearchFailed.OnNext((playerId, researchGuid, reason));
        }
    }
}