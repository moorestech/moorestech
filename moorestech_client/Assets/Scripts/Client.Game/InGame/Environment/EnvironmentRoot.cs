using CommandForgeGenerator.Command;
using UnityEngine;

namespace Client.Game.InGame.Environment
{
    public class EnvironmentRoot : MonoBehaviour, ISkitEnvironmentRoot
    {
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
    }
}