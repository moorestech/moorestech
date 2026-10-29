import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { measure, inside } from './measure.mjs';
const qa = '/Users/sakastudio/hermes-agent/data/worktrees/tier9-integrated-qa/moorestech/moorestech_web/webui';
const require = createRequire(`${qa}/package.json`);
const { test, expect } = require('@playwright/test');
const evidence = path.dirname(fileURLToPath(import.meta.url));
const locales = ['japanese', 'english', 'german', 'korean'];
const reasonKey = 'ui.blockInventory.machineWaitOutputSpaceShort';

for (const locale of locales) for (const filters of [20, 30]) for (const height of [600, 720, 1080]) for (const mode of ['inventory', 'selection']) {
  test(`${locale}-${filters}-${height}-${mode}`, async ({ page }, testInfo) => {
    const name = `${locale}-${filters}-${height}-${mode}`;
    const record = { name, locale, filters, viewport: { width: 1280, height }, mode, wire: [], consoleErrors: [], pageErrors: [] };
    let shortage = true;
    page.on('pageerror', error => record.pageErrors.push(error.message));
    page.on('console', msg => { if (msg.type() === 'error') record.consoleErrors.push(msg.text()); });
    // Unity不在のためwireだけを変換。製品の受信契約・描画・CSSをそのまま通す。
    // With Unity unavailable, change only wire data and retain production validation, rendering and CSS.
    await page.routeWebSocket('**/ws', ws => {
      const upstream = ws.connectToServer();
      upstream.onMessage(message => {
        const envelope = JSON.parse(message.toString());
        if (envelope.topic === 'localization.current') envelope.data.locale = locale;
        if (envelope.topic === 'block_inventory.current' && envelope.data.machine) {
          const before = structuredClone(envelope.data);
          envelope.data.machine.currentState = 'idle';
          envelope.data.machine.outputSpaceShort = shortage;
          const room = envelope.data.cleanRoom.room;
          room.airFilters = Array.from({ length: filters }, (_, index) => ({ ...room.airFilters[index % room.airFilters.length], x: index + 1 }));
          record.wire.push({ op: envelope.op, revision: envelope.revision, before, after: envelope.data });
        }
        ws.send(JSON.stringify(envelope));
      });
    });
    try {
      await page.setViewportSize({ width: 1280, height });
      const fixture = mode === 'selection' ? 'cleanRoomMachine20FiltersSelecting' : 'cleanRoomMachine20Filters';
      expect((await page.request.get(`/__block?type=${fixture}`)).ok()).toBe(true);
      const dictionary = await (await page.request.get(`/api/i18n/${locale}`)).json();
      await page.goto('/');
      await expect(page.getByTestId('machine-wait-reason')).toHaveText(dictionary[reasonKey]);
      await expect(page.getByTestId('machine-state-label')).toHaveText(dictionary['ui.blockInventory.machineStateIdle']);
      const bodyId = mode === 'inventory' ? 'machine-inventory-body' : 'machine-recipe-selection';
      const body = page.getByTestId(bodyId);
      const viewport = body.locator("xpath=ancestor::*[contains(@class,'mantine-ScrollArea-viewport')][1]");
      const lastFilter = page.getByTestId(`clean-room-air-filter-${filters - 1}`);
      await lastFilter.scrollIntoViewIfNeeded();
      record.lastFilter = await measure(lastFilter);
      record.filterViewport = await measure(viewport);
      expect(record.lastFilter.hit).toBe(true);
      expect(inside(record.lastFilter.rect, record.filterViewport.rect, 0.5)).toBe(true);
      const tail = body.locator(':scope > *').last();
      await tail.scrollIntoViewIfNeeded();
      record.tail = await measure(tail);
      record.body = await measure(body);
      record.scroller = await measure(viewport);
      record.panel = await measure(page.getByTestId('block-inventory'));
      record.footer = await measure(page.getByTestId('machine-wait-reason').locator('..'));
      record.footerItems = {};
      await page.screenshot({ path: path.join(evidence, 'images', `${name}.png`) });
      expect(record.scroller.clientHeight).toBeGreaterThanOrEqual(120);
      expect(record.tail.hit).toBe(true);
      expect(inside(record.tail.rect, record.scroller.rect, 0.5)).toBe(true);
      for (const id of ['machine-state-label', 'machine-power-rate', 'machine-wait-reason']) {
        const item = await measure(page.getByTestId(id));
        record.footerItems[id] = item;
        expect(item.hit, `${id} center hit`).toBe(true);
        expect(inside(item.rect, record.panel.rect, 0.5), `${id} inside panel`).toBe(true);
        expect(item.rect.bottom).toBeLessThanOrEqual(height);
        expect(record.scroller.rect.bottom).toBeLessThanOrEqual(item.rect.y + 0.5);
        expect(inside(item.textRect, item.rect, 1), `${id} text inside element`).toBe(true);
        expect(item.scrollWidth).toBeLessThanOrEqual(item.clientWidth + 1);
      }
      // 同じ受信経路でfalseへ戻し、理由が消えて待機ラベルが残ることも見る。
      // Return false through the same wire path and verify only the reason disappears.
      shortage = false;
      await page.request.get(`/__block?type=${fixture}`);
      await expect(page.getByTestId('machine-wait-reason')).toHaveCount(0);
      await expect(page.getByTestId('machine-state-label')).toHaveText(dictionary['ui.blockInventory.machineStateIdle']);
      record.falseTransition = true;
      expect(record.pageErrors).toEqual([]);
      expect(record.consoleErrors).toEqual([]);
    } finally {
      record.status = testInfo.status;
      record.errors = testInfo.errors;
      fs.writeFileSync(path.join(evidence, 'results', `${name}.json`), JSON.stringify(record, null, 2));
      await page.request.get('/__block?type=closed');
    }
  });
}
