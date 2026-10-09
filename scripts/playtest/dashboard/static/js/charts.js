// 日別の積み上げ棒グラフ（SVG）と横棒リスト（HTML）
// Daily stacked bar chart (SVG) and horizontal bar list (HTML)
import { h } from "./core.js";

const SVG_NS = "http://www.w3.org/2000/svg";
const GAP = 2;
const RADIUS = 4;

function svg(tag, attrs) {
  const el = document.createElementNS(SVG_NS, tag);
  for (const [key, value] of Object.entries(attrs)) el.setAttribute(key, value);
  return el;
}

// 系列は { key, label, color(CSS変数名) } の固定順。順序は呼び出し側が決め、件数で並べ替えない
// Series are { key, label, color (CSS var) } in a fixed order; never re-sorted by value
export function stackedDayChart(days, series, valueOf, unit, height) {
  const wrap = h("div", { class: "chart" });
  if (series.length > 1) {
    wrap.append(h("div", { class: "legend" }, series.map((s) =>
      h("span", { class: "legend-item" }, h("i", { class: "swatch", style: `background:var(${s.color})` }), s.label))));
  }
  const host = h("div", { class: "chart-host" });
  wrap.append(host);
  host.style.minHeight = `${height}px`;
  requestAnimationFrame(() => drawStack(host, days, series, valueOf, unit, height));
  return wrap;
}

function drawStack(host, days, series, valueOf, unit, height) {
  const width = Math.max(host.clientWidth, 240);
  const pad = { left: 32, right: 8, top: 8, bottom: 22 };
  const totals = days.map((day) => series.reduce((sum, s) => sum + valueOf(day, s.key), 0));
  const max = niceMax(Math.max(1, ...totals));
  const plotH = height - pad.top - pad.bottom;
  const step = (width - pad.left - pad.right) / days.length;
  const barW = Math.max(2, Math.min(24, step - 4));
  const root = svg("svg", { viewBox: `0 0 ${width} ${height}`, width, height, role: "img" });
  for (const tick of [0, max / 2, max]) {
    const y = pad.top + plotH - (tick / max) * plotH;
    root.append(svg("line", { x1: pad.left, x2: width - pad.right, y1: y, y2: y, class: "grid" }));
    const label = svg("text", { x: pad.left - 6, y: y + 4, class: "axis", "text-anchor": "end" });
    label.textContent = Number.isInteger(tick) ? tick : tick.toFixed(1);
    root.append(label);
  }
  const labelEvery = Math.ceil(days.length / Math.max(1, Math.floor(width / 56)));
  days.forEach((day, i) => {
    const x = pad.left + i * step + (step - barW) / 2;
    let base = pad.top + plotH;
    const visible = series.filter((s) => valueOf(day, s.key) > 0);
    visible.forEach((s, j) => {
      const segH = (valueOf(day, s.key) / max) * plotH;
      const isTop = j === visible.length - 1;
      const drawH = Math.max(1, segH - (j > 0 ? GAP : 0));
      const bar = svg("path", { d: barPath(x, base - segH, barW, drawH, isTop) });
      bar.style.fill = `var(${s.color})`;
      root.append(bar);
      base -= segH;
    });
    if ((days.length - 1 - i) % labelEvery === 0) {
      const label = svg("text", { x: x + barW / 2, y: height - 6, class: "axis", "text-anchor": "middle" });
      label.textContent = day.slice(5).replace("-", "/");
      root.append(label);
    }
    const hit = svg("rect", { x: pad.left + i * step, y: pad.top, width: step, height: plotH, class: "hit" });
    hit.addEventListener("mousemove", (event) => showTip(event, day, series, valueOf, unit));
    hit.addEventListener("mouseleave", hideTip);
    root.append(hit);
  });
  host.replaceChildren(root);
}

function barPath(x, y, w, hgt, roundTop) {
  const r = roundTop ? Math.min(RADIUS, w / 2, hgt) : 0;
  return `M${x},${y + hgt} V${y + r} Q${x},${y} ${x + r},${y} H${x + w - r} Q${x + w},${y} ${x + w},${y + r} V${y + hgt} Z`;
}

function niceMax(value) {
  const magnitude = 10 ** Math.floor(Math.log10(value));
  const step = [1, 1.5, 2, 3, 5, 10].find((m) => m * magnitude >= value);
  return step * magnitude;
}

function tooltip() {
  return document.getElementById("tooltip");
}

function showTip(event, day, series, valueOf, unit) {
  const tip = tooltip();
  const rows = series.map((s) => h("div", { class: "tip-row" },
    h("i", { class: "swatch", style: `background:var(${s.color})` }), s.label, h("b", null, `${valueOf(day, s.key)}${unit}`)));
  tip.replaceChildren(h("div", { class: "tip-title" }, day), ...rows);
  tip.hidden = false;
  const x = Math.min(event.clientX + 14, window.innerWidth - tip.offsetWidth - 8);
  tip.style.left = `${x}px`;
  tip.style.top = `${event.clientY + 14}px`;
}

function hideTip() {
  tooltip().hidden = true;
}

// 横棒リスト: rows は { label, value, note, mark } の表示順。最大値で幅を正規化し、mark は値の横に強調して出す
// Horizontal bar list: rows are { label, value, note, mark } in display order, widths normalised to the max; mark is highlighted by the value
export function barList(rows, unit) {
  const max = Math.max(1, ...rows.map((row) => row.value));
  return h("div", { class: "barlist" }, rows.map((row) =>
    h("div", { class: "barlist-row", title: `${row.label}: ${row.value}${unit}${row.note ? `（${row.note}）` : ""}` },
      h("span", { class: "barlist-label" }, row.label),
      h("span", { class: "barlist-track" }, h("span", { class: "barlist-fill", style: `width:${(row.value / max) * 100}%` })),
      h("span", { class: "barlist-value" }, row.mark ? h("em", null, row.mark) : null, `${row.value}${unit}`))));
}
