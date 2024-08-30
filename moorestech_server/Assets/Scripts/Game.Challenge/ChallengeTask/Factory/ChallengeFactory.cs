using System.Collections.Generic;
using Mooresmaster.Model.ChallengesModule;

namespace Game.Challenge.Task.Factory
{
    public class ChallengeFactory
    {
        public delegate IChallengeTask ChallengeTaskCreator(int playerId, ChallengeElement challengeElement);
        
        private readonly Dictionary<string,ChallengeTaskCreator> _taskCreators = new();
        
        public ChallengeFactory()
        {
            _taskCreators.Add(VanillaChallengeType.CreateItemTask,CreateItemChallengeTask.Create);
            _taskCreators.Add(VanillaChallengeType.InInventoryItemTask,InInventoryItemChallengeTask.Create);
            _taskCreators.Add(VanillaChallengeType.BlockPlaceTask,BlockPlaceChallengeTask.Create);
        }
        
        public IChallengeTask CreateChallengeTask(int playerId, ChallengeElement challengeElement)
        {
            var creator = _taskCreators[challengeElement.TaskCompletionType];
            return creator(playerId, challengeElement);
        }
    }
}