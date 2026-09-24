using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi.WireContracts
{
    // Action拒否コードのC#⇔TypeScript契約を共有フィクスチャへ集約する
    // Centralizes the C#-to-TypeScript action rejection-code contract in a shared fixture
    public class ActionErrorCodeWireContractTest
    {
        // 全Actionハンドラとdispatcherのエラーコードを過不足なく網羅する
        // Covers every error code returned by action handlers and the dispatcher exactly
        [Test]
        public void ErrorCodesFixtureCoversAllHandlerCodes()
        {
            var expected = new HashSet<string>
            {
                "unknown_action", "host_stopping", "internal_error", "unknown_error",
                "invalid_payload", "invalid_count", "invalid_slot",
                "empty_slot", "insufficient_count", "grab_not_empty",
                "invalid_index", "invalid_recipe", "recipe_locked",
                "invalid_id", "invalid_result", "no_pending_modal",
                "invalid_state", "unsupported_state",
                "invalid_guid", "research_failed", "block_not_open",
                "invalid_direction", "filter_request_failed", "unknown_entry", "unknown_locale", "already_selected",
                "stale_session", "stale_revision", "intent_not_allowed", "unknown_choice",
                "blueprint_delete_not_found", "blueprint_delete_not_unlocked", "blueprint_delete_request_failed",
                // プレイ報告（plan G）: ポーズ送信と、初回同意・前回異常終了の2ゲート
                // Play reports (plan G): pause-menu submission plus first-boot consent and previous-crash gates
                "empty_description", "invalid_kind", "invalid_page", "bundle_write_failed", "no_capture_session", "capture_pending", "already_submitted", "submit_in_flight", "already_responded", "already_acknowledged", "invalid_send", "unknown_result",
            };
            var shared = JObject.Parse(LoadFixture("error_codes.json"))["codes"].ToObject<List<string>>();

            Assert.AreEqual(shared.Count, new HashSet<string>(shared).Count, "error_codes.json に重複コードがある / duplicate codes");
            Assert.That(new HashSet<string>(shared), Is.EquivalentTo(expected), "error_codes.json が C# のエラーコード集合と不一致 / mismatch with the C# error-code set");
        }

        private static string LoadFixture(string fixtureName)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName));
        }
    }
}
