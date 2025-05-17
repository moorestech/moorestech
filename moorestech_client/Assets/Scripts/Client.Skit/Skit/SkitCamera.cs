using Client.Common;
using DG.Tweening;
using UnityEngine;

namespace Client.Skit.Skit
{
    public interface ISkitCamera
    {
        public void TweenCamera(Vector3 fromPos, Vector3 fromRot, Vector3 toPos, Vector3 toRot, float duration, Ease easing);
        
        public void SetTransform(Vector3 pos, Vector3 rot);
    }
    
    public class SkitCamera : MonoBehaviour, ISkitCamera, IGameCamera
    {
        public Camera MainCamera => camera;
        [SerializeField] private Camera camera;
        
        public void TweenCamera(Vector3 fromPos, Vector3 fromRot, Vector3 toPos, Vector3 toRot, float duration, Ease easing)
        {
            camera.transform.position = fromPos;
            camera.transform.eulerAngles = fromRot;
            
            camera.transform.DOMove(toPos, duration).SetEase(easing);
            camera.transform.DORotate(toRot, duration).SetEase(easing);
        }
        
        public void SetTransform(Vector3 pos, Vector3 rot)
        {
            camera.transform.position = pos;
            camera.transform.eulerAngles = rot;
        }
        public void SetEnabled(bool cameraEnabled)
        {
            camera.enabled = cameraEnabled;
            camera.GetComponent<AudioListener>().enabled = cameraEnabled;
        }
    }
}