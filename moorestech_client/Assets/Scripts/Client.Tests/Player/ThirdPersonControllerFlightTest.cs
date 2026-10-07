using System.Reflection;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;

namespace Client.Tests.Player
{
    /// <summary>
    ///     ThirdPersonControllerの飛行分岐（入退時の縦速度リセット・重力停止）を確認する
    ///     Verifies ThirdPersonController's flight branches (vertical reset on switch, gravity skip)
    /// </summary>
    public class ThirdPersonControllerFlightTest
    {
        private const float FallingVerticalVelocity = -10f;

        private static readonly FieldInfo VerticalVelocityField = typeof(ThirdPersonController).GetField("_verticalVelocity", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo UpdateMethod = typeof(ThirdPersonController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);

        private GameObject _playerRoot;
        private ThirdPersonController _controller;
        private StarterAssetsInputs _inputs;

        [SetUp]
        public void SetUp()
        {
            // 接地判定に当たる地面の無い空中に置く
            // Place the player in mid-air with no ground for the grounded check
            _playerRoot = new GameObject("ThirdPersonControllerFlightTestPlayer");
            _playerRoot.AddComponent<CharacterController>();
            _inputs = _playerRoot.AddComponent<StarterAssetsInputs>();
            _controller = _playerRoot.AddComponent<ThirdPersonController>();
            _controller.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_playerRoot);
        }

        [Test]
        public void 入るときも抜けるときも縦速度を0へ戻す()
        {
            VerticalVelocityField.SetValue(_controller, FallingVerticalVelocity);
            _controller.SetFlying(true);
            Assert.AreEqual(0f, (float)VerticalVelocityField.GetValue(_controller), "落下速度を持ったまま浮いた");

            VerticalVelocityField.SetValue(_controller, FallingVerticalVelocity);
            _controller.SetFlying(false);
            Assert.AreEqual(0f, (float)VerticalVelocityField.GetValue(_controller), "解除時に古い縦速度から落ち始めた");
        }

        [Test]
        public void 飛行中のUpdateは重力を積算せず押されたジャンプを捨てる()
        {
            // 非飛行時は重力で縦速度が変わることを先に確かめ、比較の前提にする
            // First confirm gravity changes the vertical speed outside flight, as the baseline
            VerticalVelocityField.SetValue(_controller, FallingVerticalVelocity);
            UpdateMethod.Invoke(_controller, null);
            Assume.That((float)VerticalVelocityField.GetValue(_controller), Is.Not.EqualTo(FallingVerticalVelocity), "Time.deltaTimeが0で重力の差が出ない");

            _controller.SetFlying(true);
            VerticalVelocityField.SetValue(_controller, FallingVerticalVelocity);
            _inputs.JumpInput(true);
            UpdateMethod.Invoke(_controller, null);

            Assert.AreEqual(FallingVerticalVelocity, (float)VerticalVelocityField.GetValue(_controller), "飛行中に重力が積算された");
            Assert.IsFalse(_inputs.jump, "飛行中の押されたジャンプが残った");
        }
    }
}
