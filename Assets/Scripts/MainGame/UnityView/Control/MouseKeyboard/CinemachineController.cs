using Cinemachine;
using UnityEngine;

namespace MainGame.UnityView.Control.MouseKeyboard
{
    public class ChinemachineController : MonoBehaviour
    {
        [SerializeField] private CinemachineFreeLook cinemachineFreeLook;

        private float currentDistance = 10f;

        private void Update()
        {
            currentDistance += InputManager.UI.SwitchHotBar.ReadValue<float>() / -100;
            currentDistance = Mathf.Clamp(currentDistance, 3, 75);
            
            //Topの高さを指定
            cinemachineFreeLook.m_Orbits[0].m_Height = currentDistance;
            //Middleの高さと半径を高さの半分に指定
            cinemachineFreeLook.m_Orbits[1].m_Height = currentDistance / 2;
            cinemachineFreeLook.m_Orbits[1].m_Radius = currentDistance / 2;
        }
    }
}