import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setBlock, setUiState } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await setBlock(page, "closed");
});

test("block inventory上のEscapeはGameScreen遷移を要求する", async ({ page }) => {
  await setBlock(page, "chest");
  await setUiState(page, "SubInventory");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();
  const before = (await payloadsOf(page, "ui_state.request")).length;
  await page.keyboard.press("Escape");

  await expect.poll(async () => (await payloadsOf(page, "ui_state.request")).slice(before)).toContainEqual({ state: "GameScreen" });
  await expect(page.getByTestId("block-inventory")).toBeHidden();
});

test("Tabはブラウザのフォーカスを動かさない", async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await page.goto("/");
  await expect(page.getByTestId("app-stage")).toBeVisible();
  const activeTagName = () => page.evaluate(() => document.activeElement?.tagName ?? null);
  const before = await activeTagName();

  // 前進・後退どちらのフォーカス移動もWeb UIの選択表示と競合するため封じる
  // Both forward and backward traversal fight the web UI's selection rendering, so both are suppressed
  await page.keyboard.press("Tab");
  expect(await activeTagName()).toBe(before);
  await page.keyboard.press("Shift+Tab");
  expect(await activeTagName()).toBe(before);
});

test("ボタンにフォーカスしてSpaceを押してもclickせずキーハンドラにも届かない", async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await page.goto("/");
  await expect(page.getByTestId("app-stage")).toBeVisible();
  await page.evaluate(() => {
    const probe = window as unknown as { __probeClicks: number; __probeKeys: string[] };
    const button = document.createElement("button");
    button.id = "space-probe-button";
    button.textContent = "probe";
    probe.__probeClicks = 0;
    probe.__probeKeys = [];
    button.addEventListener("click", () => { probe.__probeClicks += 1; });
    button.addEventListener("keydown", (event) => { probe.__probeKeys.push(event.key); });
    document.body.appendChild(button);
    button.focus();
  });
  const readProbe = () => page.evaluate(() => {
    const probe = window as unknown as { __probeClicks: number; __probeKeys: string[] };
    return { clicks: probe.__probeClicks, keys: probe.__probeKeys };
  });

  // Spaceはジャンプ専用なのでボタン押下の既定動作も要素のキーハンドラも封じる
  // Space is jump-only, so both the button-press default and the element's key handlers are cut off
  await page.keyboard.press("Space");
  expect(await readProbe()).toEqual({ clicks: 0, keys: [] });

  // Enterは従来どおり要素まで届く
  // Enter still reaches the element as before
  await page.keyboard.press("Enter");
  expect((await readProbe()).keys).toEqual(["Enter"]);
});

test("checkboxではSpaceでトグルせず、contenteditableには空白が入る", async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await page.goto("/");
  await expect(page.getByTestId("app-stage")).toBeVisible();
  await page.evaluate(() => {
    const checkbox = document.createElement("input");
    checkbox.type = "checkbox";
    checkbox.id = "space-probe-checkbox";
    const editable = document.createElement("div");
    editable.setAttribute("contenteditable", "");
    editable.id = "space-probe-editable";
    document.body.append(checkbox, editable);
    checkbox.focus();
  });

  // checkboxは文字入力欄ではないのでSpaceのトグルを封じ、空属性のcontenteditableは文字入力欄として通す
  // A checkbox is not a text field so Space toggling is suppressed; an empty-valued contenteditable is a text field and passes through
  await page.keyboard.press("Space");
  expect(await page.locator("#space-probe-checkbox").isChecked()).toBe(false);

  await page.locator("#space-probe-editable").focus();
  await page.keyboard.type("a b");
  expect(await page.locator("#space-probe-editable").textContent()).toBe("a b");
});

test("GameScreenのホイールは最新equipment値から次スロットを選ぶ", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  const equipment = page.getByTestId("equipment-slots");
  await expect(equipment).toBeVisible();
  await page.mouse.wheel(0, 100);

  // fixture の selectedEquipment:0 から1段進むと次のスロットが選ばれる
  // Stepping once from the fixture's selectedEquipment:0 selects the next slot
  await expect.poll(() => payloadsOf(page, "inventory.select_equipment")).toContainEqual({ index: 1 });
  await expect(equipment.locator("> div").nth(1)).toHaveAttribute("data-selected", "true");
});
