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

  async function findExistingIssue() {
    const issues = await github.rest.issues.listForRepo({
      owner, repo, state: 'open', labels: LABEL, per_page: 20,
    });
    return issues.data.find((i) => (i.body || '').includes(MARKER)) || null;
  }

  async function findLastGreenSha() {
    const runs = await github.rest.actions.listWorkflowRuns({
      owner, repo, workflow_id: run.workflow_id,
      event: 'schedule', status: 'success', per_page: 1,
    });
    return runs.data.workflow_runs.length > 0 ? runs.data.workflow_runs[0].head_sha : null;
  }

  async function listMergedPullRequests(baseSha, headSha) {
    if (!baseSha) return [];
    const compare = await github.rest.repos.compareCommits({
      owner, repo, base: baseSha, head: headSha,
    });
    const seen = new Map();
    for (const commit of compare.data.commits) {
      const matched = /^Merge pull request #(\d+)|\(#(\d+)\)$/m.exec(commit.commit.message);
      if (!matched) continue;
      const number = matched[1] || matched[2];
      if (!seen.has(number)) {
        seen.set(number, commit.commit.message.split('\n')[0]);
      }
    }
    return [...seen.entries()].map(([number, title]) => ({ number, title }));
  }

  async function listFailedJobs() {
    const jobs = await github.rest.actions.listJobsForWorkflowRun({
      owner, repo, run_id: run.id, per_page: 50,
    });
    return jobs.data.jobs
      .filter((j) => j.conclusion === 'failure')
      .map((j) => ({ name: j.name, url: j.html_url }));
  }

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
