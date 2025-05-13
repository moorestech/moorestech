using Client.CutScene;
using Client.Game.Common;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.Skit;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;

namespace Client.Game.Sequence
{
    public class S1InitialMovie : MonoBehaviour
    {
        public const string S1InitialMoviePlayerPrefsKey = "S1InitialMoviePlayed"; //TODo そのうち保存先をワールドに変更する
        [SerializeField] private bool forcePlay;
        
        [SerializeField] private bool playCutscene = true;
        [SerializeField] private bool playSkit = true;
        [SerializeField] private bool playBackgroundSkit = true;
        
        [SerializeField] private TimelinePlayer timelinePlayer;
        [SerializeField] private TextAsset initialSkit;
        
        [SerializeField] private PlayableAsset initialMovie;
        
        [SerializeField] private SkitManager skitManager;
        
        [SerializeField] private TextAsset backgroundSkit;
        [SerializeField] private BackgroundSkitManager backgroundSkitManager;
        
        private void Start()
        {
            return;
            if (forcePlay)
            {
                InitialMovie().Forget();
                return;
            }
            
            var hasPlayed = PlayerPrefs.GetInt(S1InitialMoviePlayerPrefsKey, 0);
            if (hasPlayed != 0) return;
            
            PlayerPrefs.SetInt(S1InitialMoviePlayerPrefsKey, 1);
            PlayerPrefs.Save();
            
            InitialMovie().Forget();
        }
        
        private async UniTask InitialMovie()
        {
            if (playCutscene)
            {
                GameStateController.ChangeState(GameStateType.CutScene);
                await timelinePlayer.Play(initialMovie);
            }
            
            if (playSkit)
            {
                GameStateController.ChangeState(GameStateType.Skit);
                await skitManager.StartSkit(initialSkit);
            }
            
            if (playBackgroundSkit)
            {
                GameStateController.ChangeState(GameStateType.InGame);
                await backgroundSkitManager.StartBackgroundSkit(backgroundSkit);
            }
        }
    }
}