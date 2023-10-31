using System;
using GameConst;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MainMenu
{
    /// <summary>
    /// プレイヤーIDがセットされてないときに、プレイヤーIDセットする
    /// </summary>
    public class SetPlayerId : MonoBehaviour
    {
        private void Start()
        {
            if (!PlayerPrefs.HasKey(PlayerPrefsKeys.PlayerIdKey))
            {
                //プレイヤーIDをランダムに生成して設定
                PlayerPrefs.SetInt(PlayerPrefsKeys.PlayerIdKey,Random.Range(2,Int32.MaxValue));
                PlayerPrefs.Save();
            }
        }
    }
}