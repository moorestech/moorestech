using System.Collections.Generic;
using Mooresmaster.Model.ChallengesModule;

namespace Game.Challenge.Task.Factory
{
    public class ChallengeFactory
    {
        public delegate IChallengeTask ChallengeTaskCreator(ChallengeMasterElement challengeElement);
        
        private readonly Dictionary<string,ChallengeTaskCreator> _taskCreators = new();
        
        public ChallengeFactory()
        {
            _taskCreators.Add(VanillaChallengeType.CreateItemTask,CreateItemChallengeTask.Create);
            _taskCreators.Add(VanillaChallengeType.InInventoryItemTask,InInventoryItemChallengeTask.Create);
            _taskCreators.Add(VanillaChallengeType.BlockPlaceTask,BlockPlaceChallengeTask.Create);
            _taskCreators.Add(VanillaChallengeType.CompleteResearchTask,CompleteResearchChallengeTask.Create);
            _taskCreators.Add(VanillaChallengeType.EquipItemTask,EquipItemChallengeTask.Create);
            _taskCreators.Add(VanillaChallengeType.BlockPlaceOnVeinTask,BlockPlaceOnVeinChallengeTask.Create);
            _taskCreators.Add(VanillaChallengeType.GearSpinningTask,GearBlockChallengeTask.Create);
            _taskCreators.Add(VanillaChallengeType.GearConnectedToTask,GearBlockChallengeTask.Create);
        }
        
        public IChallengeTask CreateChallengeTask(ChallengeMasterElement challengeElement)
        {
            var creator = _taskCreators[challengeElement.TaskCompletionType];
            return creator(challengeElement);
        }
    }
}