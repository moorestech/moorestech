// 表示用フォーマッタ
// Display formatters

const RST = '\x1b[0m';

// 使用率に応じた色付け
// Color by utilization percentage
function colorPct(pct) {
    const c = pct < 40 ? '\x1b[32m' : pct < 60 ? '\x1b[33m' : pct < 80 ? '\x1b[38;5;208m' : '\x1b[31m';
    return `${c}${pct}%${RST}`;
}

// claude-sonnet-4-6 -> Sonnet 4.6
function formatModel(model) {
    if (!model) return 'Unknown';
    if (typeof model === 'object') {
        if (model.display_name) return model.display_name.replace(/\s*\(1M context\)/, ':1M');
        model = model.id || '';
    }
    const m = model.match(/claude-(\w+)-(\d+)-(\d+)/i);
    if (m) return `${m[1][0].toUpperCase() + m[1].slice(1)} ${m[2]}.${m[3]}`;
    return model.replace(/^claude-/i, '');
}

// /a/b/c -> "c\b\a\"（現在ディレクトリから最上位へ逆順）
// /a/b/c -> "c\b\a\" (current dir first, up to root)
function formatCwdReversed(cwd) {
    const segs = String(cwd || '').split('/').filter(Boolean);
    if (!segs.length) return '/';
    return segs.reverse().join('\\') + '\\';
}

// 91800 -> "91.8K", 14000000 -> "14.0M"
function formatK(n) {
    if (n >= 1e6) return `${(n / 1e6).toFixed(1)}M`;
    if (n >= 1e3) return `${(n / 1e3).toFixed(1)}K`;
    return `${n}`;
}

// ミリ秒 -> "1h15m" or "45m"
// Milliseconds -> "1h15m" or "45m"
function formatDuration(ms) {
    const h = Math.floor(ms / 3600000);
    const m = Math.floor((ms % 3600000) / 60000);
    if (h > 0) return m > 0 ? `${h}h${m}m` : `${h}h`;
    return `${m}m`;
}

// プログレスバー: ratio=0.58, width=20 -> "████████████▒▒▒▒▒▒▒▒"
// Progress bar: ratio=0.58, width=20 -> "████████████▒▒▒▒▒▒▒▒"
function makeBar(ratio, width) {
    const filled = Math.max(0, Math.min(width, Math.round(ratio * width)));
    return '█'.repeat(filled) + '▒'.repeat(width - filled);
}

// 空白を潰して省略
// Collapse whitespace and truncate
function truncate(s, maxLen) {
    const oneline = String(s || '').replace(/\s+/g, ' ').trim();
    return oneline.length <= maxLen ? oneline : oneline.slice(0, maxLen - 1) + '…';
}

module.exports = { colorPct, formatModel, formatCwdReversed, formatK, formatDuration, makeBar, truncate };
