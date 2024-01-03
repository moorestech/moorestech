using System;
using MainGame.UnityView.Block;
using UnityEngine;

namespace MainGame.UnityView.Game
{
    
    public class WarpPlayerObject : MonoBehaviour
    {
        [SerializeField] private PlayerPosition playerPosition;
        private void LateUpdate()
        {
            if (transform.localPosition.y < -10)
            {
                playerPosition.SetPlayerPosition(new Vector2(transform.localPosition.x,transform.localPosition.z));
            }
        }
    }
}