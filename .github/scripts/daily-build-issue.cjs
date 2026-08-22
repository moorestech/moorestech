// 日次(schedule)のUnity Buildが自動再実行後も赤いとき、専用ラベル付きIssueを起票・更新する。
// 同じ失敗が続く間は新規起票せず既存Issueへコメントし、緑に戻ったら自動クローズする。
// Files or updates a labelled issue when the daily (scheduled) Unity Build stays red after auto-rerun.
// While the same failure persists it comments on the existing issue instead of opening a new one,
// and it closes the issue automatically once the daily build goes green again.

const LABEL = '日次ビルド失敗';
const MARKER = '<!-- daily-build-issue -->';

module.exports = async ({ github, context, core }) => {
  const run = context.payload.workflow_run;
  const { owner, repo } = context.repo;

  // Unity Buildの初回失敗はci-auto-rerunが無条件で再実行するため無視し、再実行後(attempt2以降)の失敗だけを見る。成功は何attempt目でも即クローズ対象にする
  // Ignore first-attempt failures since ci-auto-rerun always retries them; only observe failures from attempt 2 onward. A success closes the issue regardless of attempt number
  if (run.conclusion !== 'success' && run.run_attempt < 2) {
    core.info(`ignoring attempt ${run.run_attempt} failure for run ${run.id}; waiting for ci-auto-rerun`);
    return;
  }

  const existing = await findExistingIssue();

  if (run.conclusion === 'success') {
    if (existing) {
      await github.rest.issues.createComment({
        owner, repo, issue_number: existing.number,
        body: `日次ビルドが緑に戻ったため自動クローズします。\nrun: ${run.html_url}`,
      });
      await github.rest.issues.update({
        owner, repo, issue_number: existing.number, state: 'closed',
      });
      core.info(`closed issue #${existing.number}`);
    }
    return;
  }

  const lastGreenSha = await findLastGreenSha();
  const suspects = await listMergedPullRequests(lastGreenSha, run.head_sha);
  const failedJobs = await listFailedJobs();
  const body = renderBody({ run, lastGreenSha, suspects, failedJobs });

  if (existing) {
    await github.rest.issues.createComment({ owner, repo, issue_number: existing.number, body });
    core.info(`commented on issue #${existing.number}`);
    return;
  }

  const created = await github.rest.issues.create({
    owner, repo,
    title: `日次ビルド失敗: ${run.head_sha.slice(0, 8)}`,
    labels: [LABEL],
    body,
  });
  core.info(`created issue #${created.data.number}`);

  // ラベル一致のopen issueのうち本文にマーカーを含むものだけを対象にする（ラベル流用の無関係issueを除外）
  // Among open issues with the label, only ones whose body carries the marker count (excludes unrelated issues reusing the label)
  async function findExistingIssue() {
    const issues = await github.rest.issues.listForRepo({
      owner, repo, state: 'open', labels: LABEL, per_page: 20,
    });
    return issues.data.find((i) => (i.body || '').includes(MARKER)) || null;
  }

  // 直近の日次(schedule)成功runのhead_shaを「前回グリーン」として取得する
  // Fetches the head_sha of the most recent successful scheduled run as the "last green" baseline
  async function findLastGreenSha() {
    const runs = await github.rest.actions.listWorkflowRuns({
      owner, repo, workflow_id: run.workflow_id,
      event: 'schedule', status: 'success', per_page: 1,
    });
    return runs.data.workflow_runs.length > 0 ? runs.data.workflow_runs[0].head_sha : null;
  }

  // 前回グリーンから今回headまでのコミットログを走査し、マージPRを容疑者として抽出する
  // Scans commits between the last green baseline and the current head to extract merged PRs as suspects
  async function listMergedPullRequests(baseSha, headSha) {
    if (!baseSha) return [];
    const compare = await github.rest.repos.compareCommits({
      owner, repo, base: baseSha, head: headSha,
    });
    const seen = new Map();
    for (const commit of compare.data.commits) {
      // 「Merge pull request #N」形式か、末尾「(#N)」形式（squash merge）のどちらかにマッチさせる
      // Matches either the "Merge pull request #N" form or a trailing "(#N)" form (squash merge)
      const matched = /(?:^Merge pull request #(\d+))|(?:\(#(\d+)\)$)/m.exec(commit.commit.message);
      if (!matched) continue;
      const number = matched[1] || matched[2];
      if (!seen.has(number)) {
        seen.set(number, commit.commit.message.split('\n')[0]);
      }
    }
    return [...seen.entries()].map(([number, title]) => ({ number, title }));
  }

  // 今回runのジョブ一覧から失敗ジョブだけを抽出する
  // Extracts only the failed jobs from this run's job list
  async function listFailedJobs() {
    const jobs = await github.rest.actions.listJobsForWorkflowRun({
      owner, repo, run_id: run.id, per_page: 50,
    });
    return jobs.data.jobs
      .filter((j) => j.conclusion === 'failure')
      .map((j) => ({ name: j.name, url: j.html_url }));
  }

  // Issue本文（前回グリーン・失敗ジョブ・容疑者PR）を組み立てる
  // Assembles the issue body (last green, failed jobs, suspect PRs)
  function renderBody({ run, lastGreenSha, suspects, failedJobs }) {
    const lines = [MARKER, ''];
    lines.push(`日次ビルドが自動再実行後も失敗しました。`);
    lines.push(`- run: ${run.html_url}`);
    lines.push(`- head: \`${run.head_sha}\``);
    lines.push(`- 前回グリーン: ${lastGreenSha ? `\`${lastGreenSha}\`` : '（成功記録なし）'}`);
    lines.push('');
    lines.push('## 失敗ジョブ:');
    for (const job of failedJobs) {
      lines.push(`- [${job.name}](${job.url})`);
    }
    lines.push('');
    lines.push('## 容疑者PR:');
    if (suspects.length === 0) {
      lines.push('- （前回グリーンからの差分を特定できませんでした）');
    }
    for (const pr of suspects) {
      lines.push(`- #${pr.number} ${pr.title}`);
    }
    lines.push('');
    lines.push('前方修正で対応します。bisectは行いません（ADR 0028）。');
    return lines.join('\n');
  }
};
