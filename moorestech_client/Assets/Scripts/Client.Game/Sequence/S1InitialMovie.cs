using System;
using Client.CutScene;
using Client.Game.Common;
using Client.Game.Skit;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;

namespace Client.Game.Sequence
{
    public class S1InitialMovie : MonoBehaviour
    {
        [SerializeField] private bool forcePlay;

        [SerializeField] private TimelinePlayer timelinePlayer;
        [SerializeField] private TextAsset initialSkit;

        [SerializeField] private PlayableAsset initialMovie;
        [SerializeField] private SkitManager skitManager;

        public const string S1InitialMoviePlayerPrefsKey = "S1InitialMoviePlayed"; //TODo そのうち保存先をワールドに変更する

        private void Start()
        {
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
            GameStateController.ChangeState(GameStateType.CutScene);
            await timelinePlayer.Play(initialMovie);

            GameStateController.ChangeState(GameStateType.Skit);
            await skitManager.StartSkit(initialSkit);

            GameStateController.ChangeState(GameStateType.InGame);
        }
    }
}