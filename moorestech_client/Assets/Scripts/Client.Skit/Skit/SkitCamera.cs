using Client.Common;
using Client.Skit.Context;
using DG.Tweening;
using UnityEngine;

namespace Client.Skit.Skit
{
    public interface ISkitCamera
    {
        // 受け口を相対位置型に限定し、原点加算の抜けをコンパイルエラーへ落とす（ADR 0029）
        // Accept only the relative-position type so a missing origin addition becomes a compile error (ADR 0029)
        public void TweenCamera(SkitRelativePosition fromPos, Vector3 fromRot, SkitRelativePosition toPos, Vector3 toRot, float duration, Ease easing);
        
        public void SetTransform(SkitRelativePosition pos, Vector3 rot);
        public void SetFov(float fov);
    }
    
    public class SkitCamera : MonoBehaviour, ISkitCamera, IGameCamera
    {
        public Camera Camera => camera;
        [SerializeField] private Camera camera;
        
        private SkitOrigin _skitOrigin;
        
        // スキット開始時に再生文脈の原点を押し込み、加算はこのsinkの中だけで起きるようにする
        // Push the playback context's origin in at skit start so the addition happens only inside this sink
        public void SetSkitOrigin(SkitOrigin skitOrigin)
        {
            _skitOrigin = skitOrigin;
        }
        
        public void TweenCamera(SkitRelativePosition fromPos, Vector3 fromRot, SkitRelativePosition toPos, Vector3 toRot, float duration, Ease easing)
        {
            camera.transform.position = fromPos.ToWorld(_skitOrigin);
            camera.transform.eulerAngles = fromRot;
            
            camera.transform.DOMove(toPos.ToWorld(_skitOrigin), duration).SetEase(easing);
            camera.transform.DORotate(toRot, duration).SetEase(easing);
        }
        
        public void SetTransform(SkitRelativePosition pos, Vector3 rot)
        {
            camera.transform.position = pos.ToWorld(_skitOrigin);
            camera.transform.eulerAngles = rot;
        }
        public void SetFov(float fov)
        {
            camera.fieldOfView = fov;
        }
        
        public void SetEnabled(bool cameraEnabled)
        {
            camera.enabled = cameraEnabled;
            camera.GetComponent<AudioListener>().enabled = cameraEnabled;
        }
    }
}
