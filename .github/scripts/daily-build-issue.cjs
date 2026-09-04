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

  // cancelled/skipped/neutral等はfailureではないため無視し、failure/timed_outだけを対象にする
  // Ignores cancelled/skipped/neutral etc. as non-failures; only failure/timed_out are treated as build failures
  const isFailure = run.conclusion === 'failure' || run.conclusion === 'timed_out';
  if (!isFailure && run.conclusion !== 'success') {
    core.info(`ignoring non-terminal conclusion "${run.conclusion}" for run ${run.id}`);
    return;
  }

  // 初回失敗はci-auto-rerunが再実行するので待つが、再実行されなかったなら初回失敗でも起票する。成功は何attempt目でもクローズ対象
  // Wait out a first failure while ci-auto-rerun retries it, but file it when no retry happened; a success closes the issue at any attempt
  if (isFailure && run.run_attempt < 2 && !(await isFinalAttempt())) {
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

  // ci-auto-rerunがattemptを上げるまで待って観測する。上がらなければ再実行は行われなかったと判定する
  // Watches until ci-auto-rerun bumps the attempt; if it never does, no retry happened
  async function isFinalAttempt() {
    const POLL_INTERVAL_MS = 15000;
    const POLL_COUNT = 12;
    for (let i = 0; i < POLL_COUNT; i++) {
      await new Promise((resolve) => setTimeout(resolve, POLL_INTERVAL_MS));
      const latest = await github.rest.actions.getWorkflowRun({ owner, repo, run_id: run.id });
      if (latest.data.run_attempt > run.run_attempt) {
        core.info(`ci-auto-rerun started attempt ${latest.data.run_attempt}; leaving this run to it`);
        return false;
      }
    }
    core.info(`no rerun observed within ${(POLL_INTERVAL_MS * POLL_COUNT) / 1000}s; treating attempt ${run.run_attempt} as final`);
    return true;
  }

  // 識別は本文マーカーが正。ラベルで絞るとpollerが修復開始時にLABELを剥がした瞬間に見失い、Issueが毎日増える
  // The body marker owns identity; filtering by label loses the issue the moment the poller strips LABEL at repair start, spawning one issue per day
  async function findExistingIssue() {
    const issues = await github.paginate(github.rest.issues.listForRepo, {
      owner, repo, state: 'open', per_page: 100,
    });
    return issues.find((i) => i.pull_request === undefined && (i.body || '').includes(MARKER)) || null;
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

  // 今回runのジョブ一覧から失敗ジョブだけを抽出し、各ジョブのログ末尾（取得失敗時はnull）を添える
  // Extracts only the failed jobs from this run's job list, attaching each job's log tail (null when the fetch failed)
  async function listFailedJobs() {
    const jobs = await github.rest.actions.listJobsForWorkflowRun({
      owner, repo, run_id: run.id, per_page: 50,
    });
    const failed = jobs.data.jobs.filter((j) => j.conclusion === 'failure' || j.conclusion === 'timed_out');
    return Promise.all(failed.map(async (j) => ({
      name: j.name, url: j.html_url, logExcerpt: await fetchJobLogExcerpt(j.id),
    })));
  }

  // ジョブログの末尾4000文字を返す。取得失敗はnullで、本当に空のログ（空文字）と区別する
  // Returns the last 4000 characters of a job's log; a failed fetch yields null, distinct from a genuinely empty log
  async function fetchJobLogExcerpt(jobId) {
    const LOG_TAIL_CHARS = 4000;
    // GitHub APIとの境界。ログ取得の失敗でIssue起票そのものを落とさないため、ここだけtry-catchで隔離する
    // This is the GitHub API boundary; isolate it so a log fetch failure cannot abort the issue filing itself
    let log = '';
    try {
      const response = await github.rest.actions.downloadJobLogsForWorkflowRun({ owner, repo, job_id: jobId });
      // 302先のblobはContent-Type次第でBuffer/ArrayBufferで返るため、文字列化してから切り出す
      // The redirected blob can arrive as a Buffer/ArrayBuffer depending on Content-Type, so decode before slicing
      const data = response.data;
      log = typeof data === 'string' ? data : Buffer.from(data).toString('utf8');
    } catch (error) {
      core.warning(`failed to fetch logs for job ${jobId}: ${error.message}`);
      return null;
    }
    return log.length > LOG_TAIL_CHARS ? log.slice(-LOG_TAIL_CHARS) : log;
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
      if (job.logExcerpt === null) {
        lines.push(`- （ログ取得に失敗しました: ${job.url}）`);
      } else if (job.logExcerpt) {
        lines.push('```');
        lines.push(job.logExcerpt);
        lines.push('```');
      }
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
