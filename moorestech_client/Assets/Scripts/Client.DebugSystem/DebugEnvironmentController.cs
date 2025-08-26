using UnityEngine;

namespace Client.DebugSystem
{
    public class DebugEnvironmentController : MonoBehaviour
    {
        [SerializeField] private GameObject gameEnvironment;
        [SerializeField] private GameObject debugEnvironment;
        [SerializeField] private GameObject pureNatureEnvironment;
        
        public static void SetEnvironment(DebugEnvironmentType environmentType)
        {
            var debugEnvironmentController = FindObjectOfType<DebugEnvironmentController>();
            if (debugEnvironmentController == null)
            {
                return;
            }
            
            var debugEnvironment = false;
            var pureNatureEnvironment = false;
            var gameEnvironment = false;
            switch (environmentType)
            {
                case DebugEnvironmentType.Debug:
                    debugEnvironment = true;
                    break;
                case DebugEnvironmentType.PureNature:
                    pureNatureEnvironment = true;
                    break;
                case DebugEnvironmentType.Game:
                    gameEnvironment = true;
                    break;
            }
            
            debugEnvironmentController.debugEnvironment.SetActive(debugEnvironment);
            debugEnvironmentController.pureNatureEnvironment.SetActive(pureNatureEnvironment);
            debugEnvironmentController.gameEnvironment.SetActive(gameEnvironment);
        }
    }
    
    public enum DebugEnvironmentType
    {
        Debug,
        PureNature,
        Game,
    }
}