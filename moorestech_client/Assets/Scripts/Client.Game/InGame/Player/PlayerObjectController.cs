using Client.Game.InGame.BlockSystem;
using Client.Network.API;
using StarterAssets;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Player
{
    public interface IPlayerObjectController
    {
        public Vector3 Position { get; }
        public Vector2 Position2d { get; }
        public void SetPlayerPosition(Vector2 playerPos);
        public void SetActive(bool active);
        
        public void SetAnimationState(string state);
    }
    
    public class PlayerAnimationState
    {
        public const string IdleWalkRunBlend = "Idle Walk Run Blend";
        public const string JumpStart = "JumpStart";
        public const string JumpInAir = "JumpInAir";
        public const string JumpLand = "JumpLand";
        public const string Axe = "Axe";
    }
    
    public class PlayerObjectController : MonoBehaviour, IPlayerObjectController
    {
        public Vector3 Position => transform.position;
        public Vector2 Position2d => new(transform.position.x, transform.position.z);
        
        [SerializeField] private ThirdPersonController controller;
        [SerializeField] private Animator animator;
        
        [Inject]
        public void Construct(InitialHandshakeResponse initialHandshakeResponse)
        {
            controller.Initialize();
            SetPlayerPosition(initialHandshakeResponse.PlayerPos);
        }
        
        private void LateUpdate()
        {
            if (transform.localPosition.y < -10) SetPlayerPosition(new Vector2(transform.localPosition.x, transform.localPosition.z));
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
        
        public void SetAnimationState(string state)
        {
            animator.Play(state);
        }
    }
}