using CommandForgeGenerator.Command;
using UnityEngine;

namespace Client.Game.InGame.Environment
{
    public class EnvironmentRoot : MonoBehaviour, IEnvironmentRoot
    {
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
    }
}