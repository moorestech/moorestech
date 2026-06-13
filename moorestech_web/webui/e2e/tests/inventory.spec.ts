import { test, expect } from "@playwright/test";

type ActionRecord = { type: string; payload: unknown };

test("接続後にインベントリが描画される", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
  // Wood(itemId=1,count=10) の count バッジが出る
  // The count badge for Wood (itemId=1, count=10) appears
  await expect(page.getByText("10").first()).toBeVisible();
});

test("左クリックで grab オーバーレイが追従する", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
  const firstSlot = page.locator(".grid.grid-cols-9 > div").first();
  await firstSlot.click();
  // move_item→grab を mock がシミュレートし、grab オーバーレイ(fixed z-40)が出現する
  // The mock simulates move_item→grab so the grab overlay (fixed z-40) appears
  await expect(page.locator(".fixed.z-40")).toBeVisible();
});

test("ダブルクリックで同種を集約し、collect はクリックされたスロットを送る", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
  // Wood は main[0]=10 と main[2]=5 に分かれている。先頭をダブルクリックすると 15 へ集約される
  // Wood is split across main[0]=10 and main[2]=5; double-clicking the first slot consolidates to 15
  const firstSlot = page.locator(".grid.grid-cols-9 > div").first();
  await firstSlot.dblclick();
  // クリック連鎖と event の競合に関わらず、host が現在の grab で集積先を決め Wood は 15 にまとまる
  // Regardless of the click/event race, the host decides the target from its grab and Wood ends as 15
  await expect(page.getByText("15").first()).toBeVisible();
  // Web は target ではなくクリックされた slot を送る（grab/slot の判断は host 側）
  // The web sends the clicked slot, not a target; the grab/slot decision lives on the host
  await expect
    .poll(async () => {
      const actions: ActionRecord[] = await page.request.get("/__actions").then((r) => r.json());
      const collect = actions.find((a) => a.type === "inventory.collect");
      return collect?.payload as { slot?: { area?: string; slot?: number } } | undefined;
    })
    .toEqual({ slot: { area: "main", slot: 0 } });
  // クリック連鎖の stale な2回目 pickup は host で empty_slot 失敗するが、良性なのでトーストを出さない。
  // collect 反映(15)後に確認するので、失敗トースト(3s生存)が出ていれば下の bounded 0件アサートは失敗する
  // The stale second pickup fails with empty_slot on the host, but as a benign op it must not toast.
  // Checked after collect settles (15); a failure toast (lives 3s) would still be present and fail this bounded assert
  await expect(page.locator(".fixed.bottom-4.right-4").getByText(/failed/)).toHaveCount(0, { timeout: 2000 });
});

test("右クリックで inventory.split を送る", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
  const firstSlot = page.locator(".grid.grid-cols-9 > div").first();
  await firstSlot.click({ button: "right" });
  await expect
    .poll(async () => {
      const actions: ActionRecord[] = await page.request.get("/__actions").then((r) => r.json());
      return actions.some((a) => a.type === "inventory.split");
    })
    .toBe(true);
});
