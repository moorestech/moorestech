using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Client.Tests.Ui.TestHost
{
    /// <summary>
    /// 本物の描画装置が要る検査（GPU の描画・読み戻し・RT の作成失敗）を、描画装置の無い Editor（-nographics の batchmode）で飛ばす
    /// Skips checks that need a real graphics device (GPU drawing, read-back, RT creation failure) when the Editor has none (-nographics batchmode)
    /// </summary>
    /// <remarks>
    /// -nographics では RenderTexture の読み戻しが未初期化の値（205,205,205,205）を返し、RT の作成失敗も本物の装置の制限で確かめられないので、色や作成失敗の検査が意味を持たない。飛ばした検査は描画装置のある Editor（winunity test --graphics か Mac の uloop run-tests）で検査する
    /// Under -nographics RenderTexture read-back returns uninitialised values (205,205,205,205) and RT creation failure cannot be checked against real device limits, so colour and creation-failure checks mean nothing; skipped checks run in an Editor with a graphics device (winunity test --graphics or uloop run-tests on the Mac)
    /// </remarks>
    public static class GraphicsDeviceRequirement
    {
        public static void IgnoreWhenNoGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) return;
            Assert.Ignore("-nographics では描画装置が無い（GPU の描画・読み戻し・RT の作成を確かめられない）。winunity test --graphics か Mac の uloop run-tests で検査する");
        }
    }
}
