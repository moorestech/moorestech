// CI Auto Retry の判定本体。失敗が flaky / インフラ起因 / 原因不明に見えるときだけ失敗ジョブを再実行する。
// PR が更新されて新しい run がある場合や head ブランチが消えた場合は、古い run の再実行をスキップする。
// Decision core of CI Auto Retry: reruns failed jobs only when the failure looks flaky/infra-related/unknown.
// Skips rerunning stale runs when the PR already has a newer run or the head branch is gone.

// インフラ/flaky を示唆するステップ名のキーワード（再実行寄りに倒す）。
// Keywords in step names that hint at infra/flaky trouble (bias toward rerun).
const INFRA_KEYWORDS = [
  'checkout', 'setup', 'set up', 'install', 'restore', 'cache',
  'download', 'upload', 'artifact', 'license', 'activation', 'activate',
  'token', 'authenticate', 'auth', 'docker', 'pull image', 'network',
  'fetch', 'clone', 'runner', 'provision',
];

// 明確なコード起因（コンパイル/型/lint/テスト/アサーション）を示すステップ名キーワード。
// Keywords that indicate a clear code failure (compile/type/lint/test/assertion).
const CODE_KEYWORDS = [
  'test', 'build', 'compile', 'lint', 'typecheck', 'type check',
  'assert', 'analyze', 'analysis', 'format', 'validate',
];

// Unity Test / Unity Build はログ本文なしでは flaky と実装バグを区別できないため、初回失敗のみ無条件で再実行する。
// Unity Test / Unity Build cannot distinguish flaky failures from real bugs without log bodies, so rerun their first failure.
const RERUN_ON_FIRST_FAILURE_WORKFLOWS = ['Unity Test', 'Unity Build'];

module.exports = async ({ github, context, core }) => {
  const run = context.payload.workflow_run;
  const owner = context.repo.owner;
  const repo = context.repo.repo;

  // 無限リトライ防止: 3 回目以降の試行では何もしない。
  // Prevent infinite retries: give up once we have already attempted 3 times.
  if (run.run_attempt >= 3) {
    core.info(`run_attempt=${run.run_attempt} (>=3) のため再実行しません / skipping rerun.`);
    return;
  }

  // 失敗原因を infra / code / unknown に分類し、再実行すべきか判定する。
  // Classify failure causes into infra / code / unknown and decide whether to rerun.
  const reasons = await classifyFailureReasons();
  if (!decideRerun(reasons)) {
    return;
  }

  // 判定中に PR が更新された可能性があるため、鮮度チェックは再実行 API 呼び出しの直前に行う。
  // The PR may have been updated during evaluation, so check staleness right before the rerun API call.
  if (await isStaleRun()) {
    return;
  }

  // 失敗したジョブのみ再実行する。
  // Rerun only the failed jobs of this run.
  await github.rest.actions.reRunWorkflowFailedJobs({ owner, repo, run_id: run.id });
  core.info(`run ${run.id} の失敗ジョブを再実行しました / re-ran failed jobs.`);

  async function classifyFailureReasons() {
    const reasons = [];

    // run 全体が timed_out ならインフラ起因とみなす。
    // A run-level timed_out is treated as an infra cause.
    if (run.conclusion === 'timed_out') {
      reasons.push('infra');
    }

    if (run.run_attempt === 1 && RERUN_ON_FIRST_FAILURE_WORKFLOWS.includes(run.name)) {
      reasons.push('workflow-first-attempt');
      core.info(`workflow "${run.name}" は初回失敗を再実行対象にします / first failure is rerunnable.`);
    }

    // run に紐づく全ジョブを取得（ページング対応）。
    // Fetch every job of the run (with pagination).
    const jobs = await github.paginate(github.rest.actions.listJobsForWorkflowRunAttempt, {
      owner, repo, run_id: run.id, attempt_number: run.run_attempt, per_page: 100,
    });

    const matches = (name, keywords) => {
      const lower = (name || '').toLowerCase();
      return keywords.some((k) => lower.includes(k));
    };

    for (const job of jobs) {
      const jobConclusion = job.conclusion;
      if (jobConclusion !== 'failure' && jobConclusion !== 'timed_out' && jobConclusion !== 'cancelled') {
        continue;
      }

      // ジョブ自体が timed_out / cancelled ならインフラ起因。
      // A timed_out/cancelled job is an infra cause.
      if (jobConclusion === 'timed_out' || jobConclusion === 'cancelled') {
        reasons.push('infra');
        core.info(`job "${job.name}" は ${jobConclusion} → infra`);
        continue;
      }

      // 失敗したステップを抽出してステップ名から原因を推定する。
      // Look at the failed steps and infer the cause from their names.
      const failedSteps = (job.steps || []).filter(
        (s) => s.conclusion === 'failure' || s.conclusion === 'timed_out' || s.conclusion === 'cancelled',
      );

      const hasTimedOutStep = failedSteps.some(
        (s) => s.conclusion === 'timed_out' || s.conclusion === 'cancelled',
      );
      const hasInfraStep = failedSteps.some((s) => matches(s.name, INFRA_KEYWORDS));
      const hasCodeStep = failedSteps.some((s) => matches(s.name, CODE_KEYWORDS));

      let reason;
      if (hasTimedOutStep || hasInfraStep) {
        reason = 'infra';
      } else if (hasCodeStep) {
        reason = 'code';
      } else {
        reason = 'unknown';
      }
      reasons.push(reason);
      const stepNames = failedSteps.map((s) => s.name).join(', ') || '(no failed step metadata)';
      core.info(`job "${job.name}" failed steps=[${stepNames}] → ${reason}`);
    }
    return reasons;
  }

  // infra が 1 つでもあれば再実行、対象 workflow の初回失敗は再実行、unknown は初回試行のみ再実行、明確な code 失敗はスキップ。
  // Rerun on any infra reason, on the first failure of listed workflows, or on unknown first attempts; skip clear code failures.
  function decideRerun(reasons) {
    const hasInfra = reasons.includes('infra');
    const hasWorkflowFirstAttempt = reasons.includes('workflow-first-attempt');
    const hasUnknown = reasons.includes('unknown');

    let shouldRerun = false;
    let decisionNote = '';
    if (hasInfra) {
      shouldRerun = true;
      decisionNote = 'infra/flaky 起因を検出 / detected infra-like failure';
    } else if (hasWorkflowFirstAttempt) {
      shouldRerun = true;
      decisionNote = `${run.name} の初回失敗 / first ${run.name} failure`;
    } else if (hasUnknown && run.run_attempt === 1) {
      shouldRerun = true;
      decisionNote = '初回試行の原因不明失敗 / unknown failure on first attempt';
    } else {
      decisionNote = '明確なコード失敗のためスキップ / clear code failure, skipping';
    }
    core.info(`reasons=[${reasons.join(', ')}] attempt=${run.run_attempt} → ${decisionNote}`);
    return shouldRerun;
  }

  // stale な run の再実行は concurrency group に再参加して新しい run をキャンセルしてしまうため、ここで確実に弾く。
  // A stale rerun would re-enter the concurrency group and cancel the newer run, so filter it out here.
  async function isStaleRun() {
    // head リポジトリ消滅（フォーク削除等）なら再実行しても意味がない。
    // A deleted head repository (e.g. removed fork) makes rerunning pointless.
    const headRepo = run.head_repository;
    if (!headRepo) {
      core.info('head repository が存在しないため再実行しません / head repository is gone, skipping.');
      return true;
    }

    // head ブランチ消滅（マージ済み PR の自動削除等）もスキップ。404 以外の API エラーは判定不能として再実行を続行する。
    // Skip when the head branch is gone (e.g. auto-deleted after merge); non-404 API errors fail open and keep the rerun.
    const branchRes = await github.rest.repos.getBranch({
      owner: headRepo.owner.login,
      repo: headRepo.name,
      branch: run.head_branch,
    }).catch((error) => error);
    if (branchRes.status === 404) {
      core.info(`branch "${run.head_branch}" が存在しないため再実行しません / head branch is gone, skipping.`);
      return true;
    }
    if (!branchRes.data) {
      core.warning(`getBranch failed (status=${branchRes.status}) / 鮮度判定不能のため再実行を続行します (fail open).`);
      return false;
    }
    if (branchRes.data.commit.sha === run.head_sha) {
      return false;
    }

    // head が進んでいても新しい run が無ければ（paths-ignore 等）この run が最新の CI 結果なので再実行してよい。
    // Even when the head moved, rerun if no newer run exists (e.g. paths-ignore) because this run is still the latest CI.
    const laterRuns = await github.rest.actions.listWorkflowRuns({
      owner, repo, workflow_id: run.workflow_id, branch: run.head_branch, event: 'pull_request', per_page: 30,
    }).catch((error) => error);
    if (!laterRuns.data) {
      core.warning(`listWorkflowRuns failed (status=${laterRuns.status}) / head が進んでいるため安全側でスキップします (fail closed).`);
      return true;
    }
    const hasNewerRun = laterRuns.data.workflow_runs.some((other) =>
      other.id !== run.id
      && other.head_repository && other.head_repository.id === headRepo.id
      && new Date(other.created_at) > new Date(run.created_at));
    if (hasNewerRun) {
      core.info('同じブランチに新しい run が存在するため再実行しません / a newer run exists, skipping the stale rerun.');
      return true;
    }
    return false;
  }
};
