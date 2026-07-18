import { existsSync, readdirSync, readFileSync } from "node:fs";
import { join, resolve } from "node:path";

const background =
  "<div id=\"__worldbg\" style=\"position:fixed;inset:0;z-index:-1;pointer-events:none;background:url('/mock-orange-gradient.png') center/cover no-repeat\"></div>";
const iconDirectory = process.env.MOCK_ICON_DIR
  ?? resolve(process.cwd(), "../../../moorestech_master/server_v8/mods/moorestechAlphaMod_8/assets/item");
const iconFiles = existsSync(iconDirectory)
  ? readdirSync(iconDirectory).filter((file) => file.endsWith(".jpeg") || file.endsWith(".jpg")).sort()
  : [];
const mimeTypes: Record<string, string> = {
  ".html": "text/html", ".js": "text/javascript", ".css": "text/css", ".json": "application/json", ".png": "image/png",
};

export function injectDemoBackground(html: string, demo: boolean): string {
  if (!demo) return html;
  return html.replace(/<body(\s[^>]*)?>/i, (body) => `${body}${background}`);
}

export function placeholderIcon(itemId: number): string {
  const hue = (itemId * 47) % 360;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64"><rect x="10" y="10" width="44" height="44" rx="2" fill="hsl(${hue} 40% 52%)" stroke="hsl(${hue} 35% 34%)" stroke-width="2"/><path d="M12 52V12H52" fill="none" stroke="hsl(${hue} 45% 68%)" stroke-width="1"/><path d="M12 52H52V12" fill="none" stroke="hsl(${hue} 35% 38%)" stroke-width="1"/><rect x="18" y="20" width="28" height="9" fill="hsl(${hue} 42% 62%)"/><rect x="18" y="34" width="28" height="9" fill="hsl(${hue} 38% 44%)"/></svg>`;
}

export function realIconFor(itemId: number): Buffer | null {
  if (iconFiles.length === 0) return null;
  return readFileSync(join(iconDirectory, iconFiles[itemId % iconFiles.length]));
}

export function contentType(extension: string): string {
  return mimeTypes[extension] ?? "application/octet-stream";
}
