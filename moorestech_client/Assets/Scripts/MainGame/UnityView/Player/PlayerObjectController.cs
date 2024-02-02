using MainGame.UnityView.Block;
using StarterAssets;
using UnityEngine;

namespace MainGame.UnityView.Player
{
    public interface IPlayerObjectController
    {
        public Vector3 Position { get; }
        public Vector2 Position2d { get; }
        public void SetPlayerPosition(Vector2 playerPos);
        public void LookAt(Vector2 targetPos);
        public void SetPlayerControllable(bool enable);
        
        public void SetActive(bool active);
    }
    
    public class PlayerObjectController : MonoBehaviour, IPlayerObjectController
    {
        [SerializeField] private ThirdPersonController controller;
        [SerializeField] private StarterAssetsInputs starterAssetsInputs;

        public Vector3 Position => transform.position;
        public Vector2 Position2d => new(transform.position.x, transform.position.z);

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

        public void LookAt(Vector2 targetPos)
        {
            var target = new Vector3(targetPos.x, transform.position.y, targetPos.y);
            transform.LookAt(target);
        }

        public void SetPlayerControllable(bool enable)
        {
            starterAssetsInputs.inputEnable = enable;
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        private void LateUpdate()
        {
            if (transform.localPosition.y < -10)
            {
                SetPlayerPosition(new Vector2(transform.localPosition.x,transform.localPosition.z));
            }
        }
    }
}