using StarterAssets;
using UnityEngine;

namespace Client.Tests.Common
{
    internal static class StarterAssetsInputTestLifecycle
    {
        public static StarterAssetsInputs AddInitializedInputs(GameObject root)
        {
            // EditModeではAwakeが走らないため、実機と同じ初期化を明示する
            // EditMode does not invoke Awake, so explicitly run the runtime initialization
            var inputs = root.AddComponent<StarterAssetsInputs>();
            TestReflection.InvokePrivate(inputs, "Awake");
            TestReflection.InvokePrivate(inputs, "OnEnable");
            return inputs;
        }

        public static void ReleaseInputs(StarterAssetsInputs inputs)
        {
            // 静的イベントの購読を次のテストへ残さない
            // Never leave static event subscriptions for the next test
            TestReflection.InvokePrivate(inputs, "OnDisable");
            TestReflection.InvokePrivate(inputs, "OnDestroy");
        }
    }
}
