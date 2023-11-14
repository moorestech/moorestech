using Game.World.Interface.DataStore;
using MainGame.Basic;
using MainGame.UnityView.Block;
using StarterAssets;
using UnityEngine;

namespace MainGame.UnityView.Game
{
    public class PlayerPosition : MonoBehaviour, IPlayerPosition
    {
        [SerializeField] private ThirdPersonController controller;

        public Vector2 GetPlayerPosition()
        {
            var position = transform.position;
            return new Vector2(position.x, position.z);
        }

        /// <summary>
        ///     注意：アップデートのタイミングによってはThirdPersonController.csによる戻しが発生する可能性がある
        ///     セットしても位置が変わらなかった時はThirdPersonController.csをオフにして位置がセットできているか試してください
        /// </summary>
        /// <param name="playerPos"></param>
        public void SetPlayerPosition(Vector2 playerPos)
        {
            var height = SlopeBlockPlaceSystem.GetGroundPoint(playerPos).y;
            controller.Warp(new Vector3(playerPos.x, height, playerPos.y));
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}