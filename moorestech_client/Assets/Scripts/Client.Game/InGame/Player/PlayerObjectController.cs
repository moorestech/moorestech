using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using StarterAssets;
using UnityEngine;

namespace Client.Game.InGame.Player
{
    public interface IPlayerObjectController
    {
        public Vector3 Position { get; }
        public void SetPlayerPosition(Vector3 playerPos);
        public void SetActive(bool active);
        
        public void SetAnimationState(string state);
        public void SetControllable(bool enable);
        public void SetModelVisible(bool visible);
    }
    
    public class PlayerObjectController : MonoBehaviour, IPlayerObjectController
    {
        public Vector3 Position => transform.position;
        public Vector2 Position2d => new(transform.position.x, transform.position.z);
        
        [SerializeField] private ThirdPersonController controller;
        [SerializeField] private Animator animator;
        private readonly PlayerModelVisibility _modelVisibility = new();
        private PlayerRideFollow _rideFollow;
        private bool _isModelVisible = true;
        private Vector3 worldSpawnPosition;
        private Vector3 initialPlayerPosition;
        private bool isRuntimeStarted;

        // 参照解決と復帰先の確定まで。地形コライダーがまだ無いのでWarpも重力も始めない
        // Resolve references and settle the recovery target only; terrain colliders do not exist yet, so no warp and no gravity
        public void Initialize(Vector3 initialPlayerPosition, Vector3 worldSpawnPosition)
        {
            controller.Initialize();
            _rideFollow = new PlayerRideFollow(transform, GetComponent<CharacterController>(), controller);

            // 落下復帰先はワールドのスポーン地点。地形はランタイム構築なのでシーン配置のマーカーは当てにできない
            // Fall recovery targets the world spawn; terrain is built at runtime so a scene-authored marker cannot be trusted
            this.worldSpawnPosition = worldSpawnPosition;
            this.initialPlayerPosition = initialPlayerPosition;

            // 地形の無い空間で落下させない。重力はStartPlayerRuntimeで解禁する
            // Never let the player fall through a terrain-less space; gravity is released in StartPlayerRuntime
            controller.enabled = false;
        }

        // 地形構築の完了後にFinalizerが呼ぶ。保存座標へ置いてから重力と落下復帰を解禁する
        // Called by the finalizer once terrain is built: place at the saved position, then release gravity and fall recovery
        public void StartPlayerRuntime()
        {
            if (isRuntimeStarted) return;

            SetPlayerPosition(initialPlayerPosition);
            controller.enabled = true;
            isRuntimeStarted = true;
        }

        private void LateUpdate()
        {
            // 地形構築前は復帰先の地表が無く、復帰させると保存座標を捨ててしまう
            // Before the terrain is built there is no ground to recover onto, and recovering would discard the saved position
            if (!isRuntimeStarted) return;

            // 乗車中は補間済み車両poseを最後に反映する
            // Apply the interpolated train-car pose last while riding
            if (_rideFollow.IsFollowing())
            {
                _rideFollow.ApplyPose();
                return;
            }

            // 通常時だけ落下復帰処理を行う
            // Run fall recovery only during normal player control
            if (transform.localPosition.y < -50)
            {
                // 真下に地表があればその高さへ戻し、地形の外へ落ちたならスポーン地点へ戻す
                // Land on the ground right below when there is one; otherwise return to the spawn point after falling off the terrain
                SetPlayerPosition(ResolveFallRecoveryPosition(transform.position, worldSpawnPosition));
            }
        }

        private static Vector3 ResolveFallRecoveryPosition(Vector3 fallenPosition, Vector3 spawnPosition)
        {
            // 真下に地表があれば同じXZへ戻す
            // Return to the same XZ when ground remains below
            var fallenGround = GroundHeightProbe.GetGroundPoint(fallenPosition.x, fallenPosition.z);
            if (fallenGround.HasValue) return new Vector3(fallenPosition.x, fallenGround.Value.y, fallenPosition.z);

            // LayoutのSpawn Yではなく、そのXZにある実地表へ復帰する
            // Recover on the actual ground at the Spawn XZ instead of trusting the Layout Spawn Y
            var spawnGround = GroundHeightProbe.GetGroundPoint(spawnPosition.x, spawnPosition.z);
            return spawnGround.HasValue
                ? new Vector3(spawnPosition.x, spawnGround.Value.y, spawnPosition.z)
                : spawnPosition;
        }
        
        /// <summary>
        ///     注意：アップデートのタイミングによってはThirdPersonController.csによる戻しが発生する可能性がある
        ///     セットしても位置が変わらなかった時はThirdPersonController.csをオフにして位置がセットできているか試してください
        /// </summary>
        /// <param name="playerPos"></param>
        public void SetPlayerPosition(Vector3 playerPos)
        {
            controller.Warp(playerPos);
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        public void SetAnimationState(string state)
        {
            animator.Play(state);
        }
        public void SetControllable(bool enable)
        {
            controller.SetControllable(enable);
        }

        public void SetModelVisible(bool visible)
        {
            if (_isModelVisible == visible) return;
            _isModelVisible = visible;

            // Rendererの元状態を保存・復元する
            // Preserve each renderer state before hiding and restore only that state when showing
            if (visible) _modelVisibility.Restore();
            else _modelVisibility.Hide(GetComponentsInChildren<Renderer>(true));
        }

        // 手持ちアイテムの差し替え直後に呼ぶ。新Rendererは既定で表示のためFPSの非表示が漏れる
        // Called right after a grab item swap; new renderers default to visible and would leak through the FPS hide
        public void RefreshModelVisible()
        {
            // 非表示中の新Rendererを隠す
            // New renderers keep prefab state while visible and are hidden only while the model is hidden
            if (_isModelVisible) return;
            _modelVisibility.Hide(GetComponentsInChildren<Renderer>(true));
        }

        public void SetRideFollowTarget(Transform target, Vector3 localPosition, Quaternion localRotation)
        {
            _rideFollow.SetTarget(target, localPosition, localRotation);
            SetControllable(false);
        }

        public void ClearRideFollowTarget()
        {
            _rideFollow.ClearTarget();
            SetControllable(true);
        }
    }
}
