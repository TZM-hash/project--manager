/* Poll persisted operation jobs and update accessible progress in place. */

const terminalStatuses = new Set(["Succeeded", "Failed", "Cancelled"]);

async function refreshJob(center, card) {
  const jobId = card.dataset.jobId;
  const statusUrl = center.dataset.statusUrl || "/Operations/Status";
  const response = await fetch(`${statusUrl}?id=${encodeURIComponent(jobId)}`, {
    headers: { Accept: "application/json" },
    credentials: "same-origin"
  });
  if (!response.ok) {
    return terminalStatuses.has(card.dataset.jobStatus);
  }

  const job = await response.json();
  card.dataset.jobStatus = job.status;
  const progress = card.querySelector('[role="progressbar"]');
  const bar = card.querySelector("[data-job-progress-bar]");
  const text = card.querySelector("[data-job-progress-text]");
  const message = card.querySelector("[data-job-message]");
  const statusText = card.querySelector("[data-job-status-text]");
  const result = card.querySelector("[data-job-result]");
  const download = card.querySelector("[data-job-download]");
  if (progress) progress.setAttribute("aria-valuenow", String(job.progressPercent));
  if (bar) bar.style.width = `${job.progressPercent}%`;
  if (text) text.textContent = `${job.progressPercent}%`;
  if (message) message.textContent = job.statusMessage || "狀態已更新";
  if (statusText) statusText.textContent = job.statusText;
  if (result && job.resultSummary) {
    result.hidden = false;
    result.textContent = job.resultSummary;
  }
  if (download && job.downloadUrl) {
    download.hidden = false;
    download.href = job.downloadUrl;
  }
  return Boolean(job.isTerminal);
}

export function initOperationCenter() {
  const center = document.querySelector("[data-operation-center]");
  if (!center) return;
  const pending = new Set(
    Array.from(center.querySelectorAll("[data-operation-job]")).filter(
      (card) => !terminalStatuses.has(card.dataset.jobStatus)
    )
  );
  if (pending.size === 0) return;

  const poll = async () => {
    await Promise.all(
      Array.from(pending).map(async (card) => {
        try {
          if (await refreshJob(center, card)) pending.delete(card);
        } catch {
          // Temporary network failures are retried on the next poll.
        }
      })
    );
    if (pending.size > 0) window.setTimeout(poll, 2000);
  };
  window.setTimeout(poll, 500);
}
