using Client.Common;
using UnityEngine;

namespace Client.MainMenu
{
    /// <summary>
    ///     プレイヤーIDがセットされてないときに、プレイヤーIDセットする
    /// </summary>
    public class SetPlayerId : MonoBehaviour
    {
        private void Start()
        {
            if (!PlayerPrefs.HasKey(PlayerPrefsKeys.PlayerIdKey))
            {
                //プレイヤーIDをランダムに生成して設定
                PlayerPrefs.SetInt(PlayerPrefsKeys.PlayerIdKey, Random.Range(2, int.MaxValue));
                PlayerPrefs.Save();
            }
        }
    }
}