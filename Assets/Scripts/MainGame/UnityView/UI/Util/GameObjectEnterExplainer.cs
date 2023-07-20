using System;
using UnityEngine;

namespace MainGame.UnityView.UI.Util
{
    public class GameObjectEnterExplainer : MonoBehaviour
    {
        private void Awake()
        {
            AllGameObjectEnterExplainerController.Instance.Register(this);
        }
    }
}