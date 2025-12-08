using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// GearChainPoleの最大接続距離を視覚的に表示するコンポーネント
    /// Component for visually displaying the maximum connection distance of GearChainPole
    /// </summary>
    public class GearChainConnectRangeObject : MonoBehaviour
    {
        /// <summary>
        /// 範囲を設定する（球形表示を想定）
        /// Set the range (assuming spherical display)
        /// </summary>
        public void SetRange(float maxConnectionDistance)
        {
            // 直径として範囲を設定する
            // Set range as diameter
            var diameter = maxConnectionDistance * 2;
            transform.localScale = new Vector3(diameter, diameter, diameter);
        }

        /// <summary>
        /// 表示位置を設定する
        /// Set display position
        /// </summary>
        public void SetPosition(Vector3 position)
        {
            transform.position = position;
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}
