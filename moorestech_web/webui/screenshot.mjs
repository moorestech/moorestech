import { chromium } from 'playwright';

const browser = await chromium.launch();
const page = await browser.newPage();
await page.goto('http://localhost:5173');
await page.waitForTimeout(2000);
await page.screenshot({ path: '/private/tmp/claude-501/-Users-katsumi-moorestech/b0064f1c-7669-44cf-a2cc-a02092c4dfc1/scratchpad/full-screenshot.png' });
await browser.close();
