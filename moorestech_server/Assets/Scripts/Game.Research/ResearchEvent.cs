using System;
using Mooresmaster.Model.ResearchModule;
using UniRx;

namespace Game.Research
{
    public class ResearchEvent
    {
        // 研究完了イベント
        private readonly Subject<(int playerId, ResearchNodeMasterElement researchNode)> _onResearchCompleted = new();
        public IObservable<(int playerId, ResearchNodeMasterElement researchNode)> OnResearchCompleted => _onResearchCompleted;
        
        public void InvokeOnResearchCompleted(int playerId, ResearchNodeMasterElement researchNode)
        {
            _onResearchCompleted.OnNext((playerId, researchNode));
        }
    }
}