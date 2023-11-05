using UnityEngine;

namespace MainGame.Presenter.Tutorial
{
    /// <summary>
    ///     現在のクエストで、チュートリアルのコードを実行します
    /// </summary>
    public class TutorialExecuter : MonoBehaviour
    {
        /*
         一旦チュートリアルは実装しません！！
         将来的にまた開発するから一応残しておくだけです！！


        private const string TutorialIndexPlayerPrefsKey = "TutorialIndex";

        /// <summary>
        /// チュートリアルをその実行順番で保持するためのリスト
        /// </summary>
        private readonly List<IExecutableTutorial> _tutorials = new ();
        private int _currentTutorialIndex;

        [Inject]
        public void Construct(_0_IronMiningTutorial ironMiningTutorial,_1_MinerCraftTutorial minerCraftTutorial)
        {
            //TODO 今は手動で追加しているけど、将来的には動的に追加できるようにする
            _tutorials.Add(ironMiningTutorial);
            _tutorials.Add(minerCraftTutorial);
            _tutorials.Add(new _999_FinishTutorial());

            //TODO そのうちPlayerPrefsは脱却してサーバー側に保存したいな〜
            _currentTutorialIndex = PlayerPrefs.GetInt(TutorialIndexPlayerPrefsKey, 0);
            _tutorials[_currentTutorialIndex].StartTutorial();
        }

        private void _Update()
        {
            if (_tutorials.Count <= _currentTutorialIndex)
            {
                return;
            }

            //現在のチュートリアルが終了していたら、次のチュートリアルを実行する
            if (_tutorials[_currentTutorialIndex].IsFinishTutorial)
            {
                _tutorials[_currentTutorialIndex].EndTutorial();
                _currentTutorialIndex++;

                //TODO　チュートリアルの進捗を管理するかどうかのデバッグ機能を作ったほうがよさそう
#if !UNITY_EDITOR
                PlayerPrefs.SetInt(TutorialIndexPlayerPrefsKey, _currentTutorialIndex);
                PlayerPrefs.Save();
#endif

                //次のチュートリアルがある場合は開始メソッドを呼ぶ
                if (_tutorials.Count <= _currentTutorialIndex)
                {
                    return;
                }
                _tutorials[_currentTutorialIndex].StartTutorial();
                return;
            }

            _tutorials[_currentTutorialIndex].Update();
        }
        */
    }
}