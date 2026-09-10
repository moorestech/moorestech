# .claude/skills/moores-code-review/tests/test_init_method_naming.py
# 初期化メソッド命名規約 (Q7・PR1095人間裁定由来) の回帰テスト
# Regression test for the initialization-method naming rule (Q7, from the PR1095 human ruling)
#
# 実行: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
import sys
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent.parent / "scripts"
sys.path.insert(0, str(SCRIPTS))

import checks_moores  # noqa: E402
from patch_util import parse_patch  # noqa: E402

CLIENT = "moorestech_client/Assets/Scripts/Client.Game/View.cs"
CLIENT_TEST = "moorestech_client/Assets/Scripts/Client.Tests/ViewTest.cs"


def _patch(body: str, path: str = CLIENT) -> str:
    return (
        f"diff --git a/{path} b/{path}\n"
        f"--- a/{path}\n"
        f"+++ b/{path}\n"
        "@@ -1,0 +1,20 @@\n"
    ) + "".join(f"+{line}\n" for line in body.strip("\n").splitlines())


def _rules(patch_text: str) -> list[str]:
    return [f["rule"] for f in checks_moores.run_confirmed(parse_patch(patch_text))]


class InitMethodNamingTest(unittest.TestCase):
    def test_setup_is_confirmed(self):
        self.assertIn("init-method-naming", _rules(_patch("\n        public void Setup(Config config)\n")))

    def test_construct_is_confirmed(self):
        self.assertIn("init-method-naming", _rules(_patch("\n        public void Construct()\n")))

    def test_static_init_is_confirmed(self):
        self.assertIn("init-method-naming", _rules(_patch("\n        public static void Init()\n")))

    def test_async_setup_with_generics_is_confirmed(self):
        self.assertIn("init-method-naming", _rules(_patch("\n        private async UniTask<bool> Setup(int id)\n")))

    def test_initialize_is_clean(self):
        self.assertNotIn("init-method-naming", _rules(_patch("\n        public void Initialize(Config config)\n")))

    def test_initialize_prefix_is_clean(self):
        # InitializeSlots等の接頭辞一致は厳密名でないため対象外（意味判定はreviewer側）
        # Prefix matches like InitializeSlots are out of scope; semantic drift goes to the reviewer
        self.assertNotIn("init-method-naming", _rules(_patch("\n        public void InitializeSlots()\n")))
        self.assertNotIn("init-method-naming", _rules(_patch("\n        public void InitAsync()\n")))

    def test_override_is_exempt(self):
        # 基底の名前を継ぐしかないoverrideは対象外
        # Overrides must keep the base name, so they are exempt
        self.assertNotIn("init-method-naming", _rules(_patch("\n        public override void Setup()\n")))

    def test_test_path_is_exempt(self):
        # NUnitのSetUp等、テスト側の命名は規約対象外
        # Test-side naming (NUnit SetUp etc.) is out of scope
        self.assertNotIn("init-method-naming", _rules(_patch("\n        public void Setup()\n", CLIENT_TEST)))

    def test_local_function_is_clean(self):
        # アクセス修飾子の無いローカル関数は「生成後に呼ぶ初期化メソッド」ではない
        # Local functions without an access modifier are not post-construction initializers
        self.assertNotIn("init-method-naming", _rules(_patch("\n            void Setup()\n")))

    def test_inject_construct_is_exempt(self):
        # VContainer注入メソッドはConstruct固定の裁定済み（2026-08-30 W25/D9）
        # VContainer injection methods are ruled to stay named Construct (2026-08-30 W25/D9)
        self.assertNotIn("init-method-naming", _rules(_patch("\n        [Inject]\n        public void Construct(Store store)\n")))

    def test_plain_construct_is_still_confirmed(self):
        # 属性の無いConstructは従来どおり検出する
        # A Construct without the attribute is still detected
        self.assertIn("init-method-naming", _rules(_patch("\n        [SerializeField] private int _value;\n\n        public void Construct(Store store)\n")))

    def test_comment_mention_is_clean(self):
        self.assertNotIn("init-method-naming", _rules(_patch("\n        // public void Setup() は禁止\n")))


if __name__ == "__main__":
    unittest.main()
