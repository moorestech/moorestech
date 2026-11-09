using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Client.Tests.Input.Pointer
{
    /// <summary>
    /// プロジェクトの入力設定がホイールを 1 ノッチ＝1 に正規化することを固定する（装備のホイールの 60px 換算の前提。D-10）
    /// Pins that the project input settings normalize the wheel to one per notch (premise of the equipment wheel's 60px scaling, D-10)
    /// </summary>
    public class ScrollDeltaBehaviorSettingTest
    {
        [Test]
        public void ホイールの値は全機種で1ノッチ1に正規化される()
        {
            // 設定が変わると Windows の 120 がそのまま届き、1 ノッチで 2 段以上の量になる
            // If this changes, Windows' 120 arrives raw and one notch becomes worth several steps
            Assert.AreEqual(InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms, InputSystem.settings.scrollDeltaBehavior);
        }
    }
}
