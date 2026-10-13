import { expect, test } from "@playwright/test";
import { injectActionError, setBlock, setUiState } from "../../support/mockControl";

test.beforeEach(async ({ page }) => {
  await setBlock(page, "trainTimetable");
  await setUiState(page, "SubInventory");
  await page.goto("/");
  await page.getByTestId("train-tab-timetable").click();
});

test("駅の追加・上下移動・削除を即時反映し、再読込でも保持する", async ({ page }) => {
  const stops = page.getByTestId("train-timetable-stops").locator("li");
  const section = page.getByTestId("train-timetable-section");
  await expect(page.getByTestId("train-timetable-apply")).toHaveCount(0);
  await page.getByTestId("train-timetable-station--8_0_3-add").click();
  await expect(stops).toHaveCount(2);
  await expect(section).toHaveAttribute("aria-busy", "false");
  await page.getByTestId("train-timetable-stop-1-up").click();
  await expect(section).toHaveAttribute("aria-busy", "false");
  await expect(stops.first()).toContainText("(-8, 0, 3)");
  await page.getByTestId("train-timetable-stop-0-down").click();
  await expect(section).toHaveAttribute("aria-busy", "false");
  await expect(stops.first()).toContainText("(12, 0, 40)");
  await page.getByTestId("train-timetable-stop-0-remove").click();
  await expect(section).toHaveAttribute("aria-busy", "false");
  await expect(stops).toHaveCount(1);
  // 別タブと再読込の双方で、既に反映された一覧を確認する
  // Confirm the applied list survives both a tab switch and reloading
  await page.getByTestId("train-tab-inventory").click();
  await page.getByTestId("train-tab-timetable").click();
  await expect(stops.first()).toContainText("(-8, 0, 3)");
  await page.reload();
  await page.getByTestId("train-tab-timetable").click();
  await expect(stops).toHaveCount(1);
  await expect(stops.first()).toContainText("(-8, 0, 3)");
  await page.getByTestId("train-timetable-stop-0-remove").click();
  await expect(section).toHaveAttribute("aria-busy", "false");
  await expect(stops).toHaveCount(0);
  await expect(page.getByTestId("train-timetable-auto-run-on")).toBeDisabled();
});

test("拒否された追加を巻き戻し、次の追加で復帰する", async ({ page }) => {
  await injectActionError(page, "train_timetable.replace", "replace_failed:test");
  await page.getByTestId("train-timetable-station--8_0_3-add").click();
  await expect(page.getByTestId("train-timetable-section")).toHaveAttribute("aria-busy", "false");
  await expect(page.getByTestId("train-timetable-stops").locator("li")).toHaveCount(1);
  await page.getByTestId("train-timetable-station--8_0_3-add").click();
  await expect(page.getByTestId("train-timetable-section")).toHaveAttribute("aria-busy", "false");
  await expect(page.getByTestId("train-timetable-stops").locator("li")).toHaveCount(2);
});
