using DG.Tweening;
using UnityEngine;

namespace Client.Story
{
    public interface IStoryCamera
    {
        public void TweenCamera(Vector3 fromPos, Vector3 fromRot, Vector3 toPos, Vector3 toRot, float duration, Ease easing);

        public void SetTransform(Vector3 pos, Vector3 rot);

        public void SetActive(bool enabled);
    }

    public class StoryCamera : MonoBehaviour, IStoryCamera
    {
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

        public void SetActive(bool enabled)
        {
            gameObject.SetActive(enabled);
        }
    }
}