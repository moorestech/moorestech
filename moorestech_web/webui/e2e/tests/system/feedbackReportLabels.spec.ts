import { expect, test } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setTopicScenario, setUiState } from "../../support/mockControl";

const labels = [
  { locale: "japanese", send: "感想を送信", sent: "感想をoutboxに書き出しました", bug: "バグ報告を送信" },
  { locale: "english", send: "Send impressions", sent: "Impressions saved to outbox", bug: "Send bug report" },
  { locale: "german", send: "Eindrücke senden", sent: "Eindrücke im Postausgang gespeichert", bug: "Fehlerbericht senden" },
  { locale: "korean", send: "감상 보내기", sent: "감상이 보낼 편지함에 저장되었습니다", bug: "버그 신고 보내기" },
];

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "japanese");
  await setTopicScenario(page, "pauseConnected");
  await setUiState(page, "PlayerInventory");
});

// 本番CSVで全言語を描画し、成功後の種別リセットまで確認する
// Render every language from the production CSV and check kind reset after success
for (const label of labels) {
  test(`感想送信のボタンと完了通知: ${label.locale}`, async ({ page }) => {
    await setTopicScenario(page, "japanese");
    await setTopicScenario(page, "pauseConnected");
    await setUiState(page, "PauseMenu");
    await page.goto("/");
    await page.getByTestId("pause-menu-open-settings").click();
    await page.getByTestId(`language-select-option-${label.locale}`).click();
    await expect(page.locator("html")).toHaveAttribute("data-locale", label.locale);
    await page.getByTestId("pause-menu-back").click();
    await page.getByTestId("pause-menu-open-bug-report").click();
    await expect(page.getByTestId("bug-report-send")).toHaveText(label.bug);
    await page.getByTestId("bug-report-kind-feedback").click();
    await expect(page.getByTestId("bug-report-send")).toHaveText(label.send);
    await page.getByTestId("bug-report-description").fill("feedback label QA");
    await page.screenshot({ path: `test-results/feedback-${label.locale}.png` });
    await page.getByTestId("bug-report-send").click();
    await expect(page.getByTestId("toast-host")).toContainText(label.sent);
    await expect.poll(async () => (await payloadsOf(page, "bug_report.submit")).at(-1)).toEqual({ description: "feedback label QA", kind: "feedback" });
    await page.getByTestId("pause-menu-open-bug-report").click();
    await expect(page.getByTestId("bug-report-send")).toHaveText(label.bug);
    await expect(page.getByTestId("bug-report-description")).toHaveValue("");
  });
}
