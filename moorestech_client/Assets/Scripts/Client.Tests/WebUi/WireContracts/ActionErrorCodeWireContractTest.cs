using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi.WireContracts
{
    // Action拒否コード契約をfixtureへ集約
    // Centralizes the C#/TS action rejection-code contract in a fixture
    public class ActionErrorCodeWireContractTest
    {
        // 全エラーコードを網羅
        // Covers all error codes exactly
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
                // plan G: 送信+同意/異常終了
                // plan G: submission + consent/crash gates
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
