using Game.Challenge.TutorialParam;
using Game.Context;

namespace Client.Game.InGame.Tutorial
{
    public class TutorialManager
    {
        private MapObjectPin _mapObjectPin;
        
        public TutorialManager(MapObjectPin mapObjectPin)
        {
            _mapObjectPin = mapObjectPin;
        }
        
        public void ApplyTutorial(int challengeId)
        {
            // TODO
            var challenge = ServerContext.ChallengeConfig.GetChallenge(challengeId);
            foreach (var tutorial in challenge.Tutorials)
            {
                switch (tutorial.TutorialType)
                {
                    case MapObjectPinTutorialParam.TaskCompletionType:
                        _mapObjectPin.ApplyTutorial((MapObjectPinTutorialParam)tutorial.Param);
                        break;
                }
            }
        }
    }
}