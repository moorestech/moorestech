// DerivedTokens.uss は手書きで変換器を通らないため、半透明の表の行をここで持つ。式で決める行は Web の色から UITK の値を計算する
// DerivedTokens.uss is hand-written and bypasses the converter, so its translucent-table rows live here; formula rows compute the UITK value from the Web color
import { blackDimMethod, correctAlphaForLinearBlend, correctBlackDimAlpha, translucencyMethod } from "./translucency.mjs";
import { formatColor } from "./cssColors.mjs";

const exactMethod = "面色を背景にした厳密式（S7）";

// formula: [r, g, b, a] は S7 の式で補正する Web の色。uss は式に依らず決めた値
// formula: [r, g, b, a] is a Web color corrected by the S7 formula; uss is a value settled without the formula
const rowSources = [
  { name: "--game-panel-title-shadow", web: "rgba(0, 0, 0, 0.35)", uss: "rgba(0, 0, 0, 0.3735)", method: exactMethod },
  { name: "--game-panel-rule-top-start", web: "rgba(146, 148, 167, 0.42)", uss: "rgba(146, 148, 167, 0.1961)", method: exactMethod },
  { name: "--game-panel-rule-top-peak", web: "rgba(146, 148, 167, 0.59)", uss: "rgba(146, 148, 167, 0.3602)", method: exactMethod },
  { name: "--game-panel-rule-top-end", web: "rgba(146, 148, 167, 0.38)", uss: "rgba(146, 148, 167, 0.1653)", method: exactMethod },
  { name: "--game-panel-rule-bottom-start", web: "rgba(128, 132, 159, 0.3)", uss: "rgba(128, 132, 159, 0.1178)", method: exactMethod },
  { name: "--game-panel-rule-bottom-peak", web: "rgba(128, 132, 159, 0.42)", uss: "rgba(128, 132, 159, 0.2034)", method: exactMethod },
  { name: "--game-panel-rule-bottom-end", web: "rgba(128, 132, 159, 0.26)", uss: "rgba(128, 132, 159, 0.0946)", method: exactMethod },
  { name: "--game-panel-triangle-face", web: "rgba(94, 100, 128, 0.2)", uss: "rgba(94, 100, 128, 0.0764)", method: exactMethod },
  { name: "--panel-action-hover-face", web: "color-mix(in srgb, var(--gauge-track), var(--bevel-c2) 22%)` = `rgba(35, 38, 51, 0.844)", formula: [35, 38, 51, 0.844], method: translucencyMethod },
  { name: "--mode-switch-selected-face", web: "color-mix(in srgb, var(--gauge-track), var(--bevel-c2) 30%)` = `rgba(43, 46, 59, 0.86)", formula: [43, 46, 59, 0.86], method: translucencyMethod },
  { name: "--panel-action-text-outline", web: "rgb(0 0 0 / 70%)", formula: [0, 0, 0, 0.7], method: translucencyMethod },
  { name: "--pause-menu-face", web: "rgb(16 20 28 / 92%)", formula: [16, 20, 28, 0.92], method: `${translucencyMethod}。枠の面とボタンの面（\`--gauge-track\`）の見分けの撮影（https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-11-06-2026-11-06-uitk-screens-fix-pause-top-720p/fix-pause-top-720p.png ）は旧値 0.9961・地 0.971 のもの` },
  { name: "--theme-scroller-thumb-face", web: "rgba(255, 255, 255, 0.4)", formula: [255, 255, 255, 0.4], method: `${translucencyMethod}（Mantine ScrollArea の暗色のつまみ）` },
  { name: "--game-panel-craft-face", web: "rgb(7 9 18 / 80%)", formula: [7, 9, 18, 0.8], method: `${translucencyMethod}（GamePanel craft の面。C2-a・I-fix1 のランタイム実測）` },
  { name: "--game-panel-craft-rim", web: "rgb(146 148 167 / 55%)", formula: [146, 148, 167, 0.55], method: `${translucencyMethod}（GamePanel craft の内周。C2-a）` },
  { name: "--slot-frame-face", web: "rgb(50 52 67 / 50%)", uss: "rgba(50, 52, 67, 0.42)", method: "ランタイムの Linear 合成で持ち物の空きマスを Web の画素と比べて選んだ（C3-a Task 8。地 (36,31,34) で Web (43,41,50)・UITK (42,40,49)。面が地より明るいので暗い色の式 0.7226 は (46,47,59) に外れる。台帳 LX-23）" },
  { name: "--mantine-color-blue-light-hover", web: "rgba(34, 139, 230, 0.2)", formula: [34, 139, 230, 0.2], method: `${translucencyMethod}（Mantine Button Subtle のホバー。C2-b。明暗を最大成分で分けるようにして式の行にした。I-fix2）` },
  { name: "--screen-backdrop-face", web: "rgb(0 0 0 / 42%)", blackDimAlpha: 0.42, method: `${blackDimMethod}（画面の幕。S2-hud・設計書 A-55・A-130）` },
  { name: "--train-riding-hud-face", web: "color-mix(in srgb, var(--mantine-color-dark-8) 82%, transparent)` = `rgba(31, 31, 31, 0.82)", formula: [31, 31, 31, 0.82], method: `${translucencyMethod}（列車乗車 HUD の Paper の面。S2-hud・設計書 A-55）` },
  { name: "--modal-backdrop-face", web: "Mantine Modal.Overlay backgroundOpacity={0.6} の #000 = rgba(0, 0, 0, 0.6)", blackDimAlpha: 0.6, method: `${blackDimMethod}（Mantine Modal の幕。C3-b・A-130）` },
  { name: "--modal-shadow-1-color", web: "Mantine --mantine-shadow-xl 層 1 の rgba(0, 0, 0, 0.05)", formula: [0, 0, 0, 0.05], method: `${translucencyMethod}（Mantine Modal の影。C3-b）` },
  { name: "--modal-shadow-2-color", web: "Mantine --mantine-shadow-xl 層 2 の rgba(0, 0, 0, 0.05)", formula: [0, 0, 0, 0.05], method: `${translucencyMethod}（Mantine Modal の影。C3-b）` },
  { name: "--modal-shadow-3-color", web: "Mantine --mantine-shadow-xl 層 3 の rgba(0, 0, 0, 0.04)", formula: [0, 0, 0, 0.04], method: `${translucencyMethod}（Mantine Modal の影。C3-b）` },
  { name: "--progress-arrow-glyph-shadow", web: "drop-shadow(0 1px 1px rgb(0 0 0 / 45%)) の色", formula: [0, 0, 0, 0.45], method: `${translucencyMethod}（ProgressArrowGlyph の drop-shadow。輪の頂点ごとの濃さは部品が OpacityAt で解き直す。C4-b）` },
];

export const handWrittenDerivedRows = rowSources.map((row) => ({ name: row.name, web: row.web, uss: row.uss ?? derivedValue(row), method: row.method }));

// formula は S7 の式、blackDimAlpha は黒の幕の式で値を出す
// formula uses the S7 formula and blackDimAlpha the black-dim formula
function derivedValue(row) {
  if (row.blackDimAlpha !== undefined) return formatColor({ r: 0, g: 0, b: 0, a: correctBlackDimAlpha(row.blackDimAlpha) });
  return formulaValue(row.formula);
}

function formulaValue([red, green, blue, alpha]) {
  return formatColor({ r: red, g: green, b: blue, a: correctAlphaForLinearBlend(red, green, blue, alpha) });
}
