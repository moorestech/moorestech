using System.Collections.Generic;

namespace Game.Challenge.Task.Factory
{
    public class ChallengeFactory
    {
        public delegate IChallengeTask ChallengeTaskCreator(int playerId, ChallengeInfo config);
        
        private readonly Dictionary<string,ChallengeTaskCreator> _taskCreators = new();
        
        public ChallengeFactory()
        {
            _taskCreators.Add(CreateItemTaskParam.TaskCompletionType,CreateItemChallengeTask.Create);
            _taskCreators.Add(InInventoryItemTaskParam.TaskCompletionType,InInventoryItemChallengeTask.Create);
            _taskCreators.Add(BlockPlaceTaskParam.TaskCompletionType,BlockPlaceChallengeTask.Create);
        }
        
        public IChallengeTask CreateChallengeTask(int playerId, ChallengeInfo config)
        {
            var creator = _taskCreators[config.TaskCompletionType];
            return creator(playerId, config);
        }
    }
}