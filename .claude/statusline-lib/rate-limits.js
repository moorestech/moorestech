// レート制限行（5h / 7d）。アカウントによって five_hour が無い（5h 制限なし）ので、
// あるウィンドウだけを描く。rate_limits 自体が無いのは最初の API 応答前だけ。
// Rate-limit line. Some accounts have no five_hour window; render whichever windows exist.

const { colorPct, formatDuration, makeBar } = require('./format');

const BAR_WIDTH = 20;
const FIVE_HOUR_MS = 5 * 3600000;
const DAY_MS = 86400000;
// "Compact:" / "Session:" と ":" の位置を揃えるため、ラベルと ":" の間を空ける
const LABEL_WEEKLY = 'Weekly : ';

function buildRateLimitLine(now, rateLimits) {
    const five = rateLimits?.five_hour;
    const seven = rateLimits?.seven_day;
    if (!five && !seven) return 'Session: waiting for data...';

    const parts = [];
    if (five) parts.push(formatFiveHour(now, five));
    if (seven) parts.push(formatSevenDay(now, seven, !five));
    return parts.join(' | ');
}

// 5h 窓: バー + 使用率 + 窓内経過時間
function formatFiveHour(now, w) {
    const util = Math.round(w.used_percentage ?? 0);
    // resets_at は Unix epoch 秒。無ければ窓が今始まった扱い
    const windowEnd = w.resets_at ? w.resets_at * 1000 : now + FIVE_HOUR_MS;
    const elapsed = clamp(now - (windowEnd - FIVE_HOUR_MS), 0, FIVE_HOUR_MS);
    return `Session: ${makeBar(util / 100, BAR_WIDTH)} [${colorPct(util)}] ${formatDuration(elapsed)}/5h`;
}

// 7d 窓: 5h が無いときはこちらにバーを付けて主役にする
function formatSevenDay(now, w, withBar) {
    const util = Math.round(w.used_percentage ?? 0);
    const days = w.resets_at ? clamp(Math.ceil(7 - (w.resets_at * 1000 - now) / DAY_MS), 0, 7) : '?';
    const bar = withBar ? `${LABEL_WEEKLY}${makeBar(util / 100, BAR_WIDTH)} ` : '';
    return `${bar}[${colorPct(util)}] ${days}/7d`;
}

function clamp(v, lo, hi) {
    return Math.max(lo, Math.min(v, hi));
}

module.exports = { buildRateLimitLine };
