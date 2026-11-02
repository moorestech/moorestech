// 外部情報の取得（git・トランスクリプト）
// External sources (git, transcript)

const { execSync } = require('child_process');
const fs = require('fs');

const EXEC_OPTS = { encoding: 'utf8', stdio: ['pipe', 'pipe', 'pipe'] };

function sumTokens(usage) {
    if (!usage) return 0;
    return (usage.input_tokens || 0) + (usage.output_tokens || 0) +
           (usage.cache_creation_input_tokens || 0) + (usage.cache_read_input_tokens || 0);
}

// silent-ok: git 外のディレクトリでは枝表示が無いだけ / 表示から枝が消えるのでユーザーが気づく
function getGitInfo(cwd) {
    try {
        const branch = execSync('git rev-parse --abbrev-ref HEAD', { cwd, ...EXEC_OPTS }).trim();
        const gitEnv = { ...process.env, GIT_OPTIONAL_LOCKS: '0' };
        const status = execSync('git status --porcelain', { cwd, ...EXEC_OPTS, env: gitEnv }).trim();
        const untracked = status ? status.split('\n').filter(l => l.startsWith('??')).length : 0;
        return `🌿 ${branch}` + (untracked > 0 ? ` +${untracked}` : '');
    } catch {
        return null;
    }
}

// 最初のユーザーメッセージと最新の usage を取る
// First user message and latest usage
function analyzeTranscript(transcriptPath) {
    const empty = { firstUserMessage: '', lastUsage: null };
    // silent-ok: トランスクリプト未作成の起動直後は空表示 / 4行目と Compact が空のままなのでユーザーが気づく
    try {
        if (!transcriptPath || !fs.existsSync(transcriptPath)) return empty;
        const lines = fs.readFileSync(transcriptPath, 'utf8').split('\n').filter(l => l.trim());
        let firstUserMessage = '';
        let lastUsage = null;
        for (const line of lines) {
            let entry;
            // silent-ok: 書き込み途中の末尾行は JSON として壊れている / 次回描画で読み直される
            try { entry = JSON.parse(line); } catch { continue; }
            if (entry.isSidechain || entry.isApiErrorMessage) continue;
            if (!firstUserMessage && entry.type === 'user') firstUserMessage = extractMessageText(entry);
            if (entry.type === 'assistant' && sumTokens(entry.message?.usage) > 0) lastUsage = entry.message.usage;
        }
        return { firstUserMessage, lastUsage };
    } catch {
        return empty;
    }
}

function extractMessageText(entry) {
    const msg = entry?.message;
    if (!msg) return '';
    if (typeof msg.text === 'string' && msg.text.trim()) return msg.text;
    const content = msg.content;
    if (typeof content === 'string') return content;
    if (Array.isArray(content)) {
        return content
            .map(b => (b?.type === 'text' && typeof b.text === 'string') ? b.text : (typeof b === 'string' ? b : ''))
            .join('');
    }
    return '';
}

module.exports = { sumTokens, getGitInfo, analyzeTranscript };
