using System;
using Game.Context;
using Game.Research;
using Mooresmaster.Model.ChallengesModule;
using Mooresmaster.Model.ResearchModule;
using UniRx;

namespace Game.Challenge.Task
{
    public class CompleteResearchChallengeTask : IChallengeTask
    {
        public ChallengeMasterElement ChallengeMasterElement { get; }
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();

        private bool _completed;
        private bool _initialCheckDone;

        private readonly CompleteResearchTaskParam _completeResearchTaskParam;
        private readonly IResearchDataStore _researchDataStore;

        public static IChallengeTask Create(ChallengeMasterElement challengeMasterElement)
        {
            return new CompleteResearchChallengeTask(challengeMasterElement);
        }

        public CompleteResearchChallengeTask(ChallengeMasterElement challengeMasterElement)
        {
            ChallengeMasterElement = challengeMasterElement;

            // マスタのtaskParam型不整合を生成時に検出する（前例: InInventoryItemChallengeTask）
            // Detect a taskParam type mismatch at construction time (precedent: InInventoryItemChallengeTask)
            _completeResearchTaskParam = (CompleteResearchTaskParam)challengeMasterElement.TaskParam;
            _researchDataStore = ServerContext.GetService<IResearchDataStore>();

            var researchEvent = ServerContext.GetService<ResearchEvent>();
            researchEvent.OnResearchCompleted.Subscribe(OnResearchCompleted);
        }

        private void OnResearchCompleted((int playerId, ResearchNodeMasterElement researchNode) research)
        {
            if (_completed) return;
            if (research.researchNode.ResearchNodeGuid != _completeResearchTaskParam.ResearchNodeGuid) return;

            _completed = true;
            _onChallengeComplete.OnNext(this);
        }

        public void ManualUpdate()
        {
            // チャレンジ開始前に完了済みの研究を初回tickだけ照会して取りこぼしを防ぐ
            // Query once on the first tick to recover research completed before this challenge started
            if (_completed || _initialCheckDone) return;
            _initialCheckDone = true;

            if (!_researchDataStore.IsResearchCompleted(_completeResearchTaskParam.ResearchNodeGuid)) return;

            _completed = true;
            _onChallengeComplete.OnNext(this);
        }
    }
}
