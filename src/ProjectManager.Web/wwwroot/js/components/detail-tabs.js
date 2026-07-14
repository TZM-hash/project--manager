/* Client-enhanced detail tabs. */

function initDetailTabs() {
  document.querySelectorAll("[data-detail-tabs]").forEach((shell) => {
    const tabs = Array.from(shell.querySelectorAll("[data-detail-tab-target]"));
    const panels = Array.from(shell.querySelectorAll("[data-detail-tab-panel]"));

    if (tabs.length === 0 || panels.length === 0) {
      return;
    }

    shell.classList.add("detail-tabs-enabled");

    const activate = (target) => {
      tabs.forEach((tab) => {
        const active = tab.getAttribute("data-detail-tab-target") === target;
        tab.classList.toggle("is-active", active);
        tab.setAttribute("aria-selected", active ? "true" : "false");
      });

      panels.forEach((panel) => {
        const active = panel.getAttribute("data-detail-tab-panel") === target;
        panel.classList.toggle("is-active", active);
        panel.hidden = !active;
      });
    };

    tabs.forEach((tab) => {
      tab.setAttribute("role", "tab");
      tab.addEventListener("click", () => {
        const target = tab.getAttribute("data-detail-tab-target");
        if (target) {
          activate(target);
        }
      });
    });

    panels.forEach((panel) => {
      panel.setAttribute("role", "tabpanel");
    });

    const params = new URLSearchParams(window.location.search);
    const tabParam = params.get("Tab");
    const shouldOpenAudit = ["AuditKeyword", "AuditAction", "AuditFrom", "AuditTo"].some((key) => params.has(key));
    const shouldOpenGantt = tabParam === "gantt" || Boolean(shell.querySelector('[data-detail-tab-panel="gantt"] .alert'));
    const initial = shouldOpenGantt
      ? tabs.find((tab) => tab.getAttribute("data-detail-tab-target") === "gantt") ?? tabs[0]
      : shouldOpenAudit
        ? tabs.find((tab) => tab.getAttribute("data-detail-tab-target") === "audit") ?? tabs[0]
        : tabs.find((tab) => tab.classList.contains("is-active")) ?? tabs[0];
    activate(initial.getAttribute("data-detail-tab-target"));
  });
}

export function initDetailsTabs() {
  initDetailTabs();
}
