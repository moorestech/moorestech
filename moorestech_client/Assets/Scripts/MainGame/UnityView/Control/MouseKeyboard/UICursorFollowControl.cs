using UnityEngine;

namespace MainGame.UnityView.Control.MouseKeyboard
{
    public class UICursorFollowControl : MonoBehaviour
    {
        [SerializeField] private Vector3 offSet = Vector3.zero;
        private RectTransform _canvasRect;

        private void Start()
        {
            _canvasRect = transform.root.GetComponentsInChildren<RectTransform>()[0];
        }

        private void Update()
        {
            var magnification = _canvasRect.sizeDelta.x / Screen.width;

            var itemPos = new Vector3();

            itemPos.x = Input.mousePosition.x * magnification - _canvasRect.sizeDelta.x / 2;
            itemPos.y = Input.mousePosition.y * magnification - _canvasRect.sizeDelta.y / 2;
            itemPos.z = transform.localPosition.z;

            transform.localPosition = itemPos + offSet;
        }
    }
}