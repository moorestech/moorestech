using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Client.Tests.Ui.TestHost
{
    /// <summary>
    /// GPU で描いて画素を読み戻す検査を、描画装置の無い Editor（-nographics の batchmode）で飛ばす
    /// Skips checks that draw on the GPU and read pixels back when the Editor has no graphics device (-nographics batchmode)
    /// </summary>
    /// <remarks>
    /// -nographics では RenderTexture の読み戻しが未初期化の値（205,205,205,205）を返し、色の検査が意味を持たない。飛ばした検査は描画装置のある Mac の Editor（uloop run-tests）で検査する
    /// Under -nographics RenderTexture read-back returns uninitialised values (205,205,205,205), so colour checks mean nothing; skipped checks are run in the Mac Editor with a graphics device (uloop run-tests)
    /// </remarks>
    public static class GraphicsDeviceRequirement
    {
        public static void IgnoreWhenNoGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) return;
            Assert.Ignore("-nographics では描画できない（GPU の読み戻しが未初期化の値を返す）。描画装置のある Mac の Editor で検査する");
        }
    }
}
