using System;
using UnityEngine;

namespace MainGame.UnityView.Game
{
    public class PlayerPosition : MonoBehaviour,IPlayerPosition
    {
        public Vector2 GetPlayerPosition()
        {
            var position = transform.position;
            return new Vector2(position.x, position.z);
        }

        public void SetPlayerPosition(Vector2 vector2)
        {
            //サーバー側は2次元なのでx,yだ、unityはy upなのでzにyを入れる
            gameObject.transform.position = new Vector3(vector2.x, transform.position.y, vector2.y);
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}