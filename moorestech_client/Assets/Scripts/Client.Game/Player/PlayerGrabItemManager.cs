using System;
using UnityEngine;

namespace MainGame.UnityView.Player
{
    public class PlayerGrabItemManager : MonoBehaviour
    {
        [SerializeField] private Transform leftHandParent;
        [SerializeField] private Transform rightHandParent;

        public void SetItem(GameObject item,bool isLeft,Vector3 position = default,Quaternion rotation = default)
        {
            var parent = isLeft ? leftHandParent : rightHandParent;
            item.transform.SetParent(parent);
            item.transform.localPosition = position;
            item.transform.localRotation = rotation;
        }
    }
}