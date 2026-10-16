using Client.Input;
using NUnit.Framework;

namespace Client.Tests.Player
{
    public class SprintInputResolverTest
    {
        [Test]
        public void 押下中にホールドへ切り替えると走りを維持する()
        {
            var resolver = new SprintInputResolver(SprintInputMode.Toggle);
            resolver.OnSprintPressed();
            resolver.SetMode(SprintInputMode.Hold);
            Assert.IsTrue(resolver.IsSprinting);
            resolver.OnSprintReleased();
            Assert.IsFalse(resolver.IsSprinting);
        }

        [Test]
        public void 方式変更前の押下を離してもトグルを反転しない()
        {
            var resolver = new SprintInputResolver(SprintInputMode.Hold);
            resolver.OnSprintPressed();
            resolver.SetMode(SprintInputMode.Toggle);
            resolver.OnSprintReleased();
            Assert.IsTrue(resolver.IsSprinting);
        }

        [Test]
        public void ホールドの入力再同期は実際の押下に揃う()
        {
            var resolver = new SprintInputResolver(SprintInputMode.Hold);
            resolver.ResyncHeld(true);
            Assert.IsTrue(resolver.IsSprinting);
            resolver.ResyncHeld(false);
            Assert.IsFalse(resolver.IsSprinting);
        }

        [Test]
        public void トグルは開始時に走りで単独タップで反転する()
        {
            var resolver = new SprintInputResolver(SprintInputMode.Toggle);
            Assert.IsTrue(resolver.IsSprinting);

            resolver.OnSprintPressed();
            Assert.IsTrue(resolver.IsSprinting);
            resolver.OnSprintReleased();
            Assert.IsFalse(resolver.IsSprinting);
        }

        [Test]
        public void トグルは押下中に他ボタンが押されたら反転しない()
        {
            var resolver = new SprintInputResolver(SprintInputMode.Toggle);
            resolver.OnSprintPressed();
            resolver.OnOtherButtonPressed();
            resolver.OnSprintReleased();
            Assert.IsTrue(resolver.IsSprinting);
        }

        [Test]
        public void トグルは抑止中のタップで反転しない()
        {
            var resolver = new SprintInputResolver(SprintInputMode.Toggle);
            resolver.SetToggleAllowed(false);
            resolver.OnSprintPressed();
            resolver.OnSprintReleased();
            Assert.IsTrue(resolver.IsSprinting);
        }

        [Test]
        public void 入力無効を挟んだ押下は離しで反転しない()
        {
            // 無効中の押下を離しても反転しない
            // Releasing an unseen press must not toggle sprint
            var resolver = new SprintInputResolver(SprintInputMode.Toggle);
            resolver.ResyncHeld(true);
            resolver.OnSprintReleased();
            Assert.IsTrue(resolver.IsSprinting);
        }

        [Test]
        public void ホールドは押下中だけ走る()
        {
            var resolver = new SprintInputResolver(SprintInputMode.Hold);
            Assert.IsFalse(resolver.IsSprinting);
            resolver.OnSprintPressed();
            resolver.OnOtherButtonPressed();
            Assert.IsTrue(resolver.IsSprinting);
            resolver.OnSprintReleased();
            Assert.IsFalse(resolver.IsSprinting);
        }

        [Test]
        public void ホールドからトグルへ切り替えると走りONから始まる()
        {
            var resolver = new SprintInputResolver(SprintInputMode.Hold);
            resolver.SetMode(SprintInputMode.Toggle);
            Assert.IsTrue(resolver.IsSprinting);
        }

        [Test]
        public void トグルからホールドへ切り替えると押下中かどうかに揃う()
        {
            var resolver = new SprintInputResolver(SprintInputMode.Toggle);
            resolver.SetMode(SprintInputMode.Hold);
            Assert.IsFalse(resolver.IsSprinting);
        }
    }
}
