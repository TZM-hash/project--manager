window.toggleSidebar = function () {
  document.body.classList.toggle("sidebar-collapsed");
};

window.toggleNavGroup = function (el) {
  const item = el.closest(".nav-group");
  if (item) {
    item.classList.toggle("nav-group-open");
  }
};

document.addEventListener("DOMContentLoaded", () => {
  initBulkSelection();
  initDetailTabs();
  initRevealAnimations();
});

function initBulkSelection() {
  const checkboxes = Array.from(document.querySelectorAll("[data-bulk-checkbox]"));
  const master = document.querySelector("[data-bulk-check-all]");
  const forms = Array.from(document.querySelectorAll("[data-bulk-form]"));
  const counters = Array.from(document.querySelectorAll("[data-selected-count]"));

  if (checkboxes.length === 0 && forms.length === 0) {
    return;
  }

  const selectable = () => checkboxes.filter((box) => !box.disabled);
  const selected = () => selectable().filter((box) => box.checked);

  const updateState = () => {
    const selectedCount = selected().length;
    const selectableCount = selectable().length;

    counters.forEach((counter) => {
      counter.textContent = selectedCount.toString();
    });

    forms.forEach((form) => {
      form.querySelectorAll("[data-bulk-requires-selection]").forEach((button) => {
        button.disabled = selectedCount === 0;
      });
    });

    if (master) {
      master.checked = selectableCount > 0 && selectedCount === selectableCount;
      master.indeterminate = selectedCount > 0 && selectedCount < selectableCount;
      master.disabled = selectableCount === 0;
    }

    document.body.classList.toggle("has-bulk-selection", selectedCount > 0);
  };

  if (master) {
    master.addEventListener("change", () => {
      selectable().forEach((box) => {
        box.checked = master.checked;
      });
      updateState();
    });
  }

  checkboxes.forEach((box) => box.addEventListener("change", updateState));

  forms.forEach((form) => {
    form.addEventListener("submit", (event) => {
      const ids = selected().map((box) => box.value);
      if (ids.length === 0) {
        event.preventDefault();
        window.alert("请至少选择一项。");
        return;
      }

      const message = form.getAttribute("data-confirm-message");
      if (message && !window.confirm(message)) {
        event.preventDefault();
        return;
      }

      form.querySelectorAll("[data-bulk-id]").forEach((input) => input.remove());
      ids.forEach((id) => {
        const input = document.createElement("input");
        input.type = "hidden";
        input.name = "ids";
        input.value = id;
        input.setAttribute("data-bulk-id", "true");
        form.appendChild(input);
      });
    });
  });

  updateState();
}

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

    const initial = tabs.find((tab) => tab.classList.contains("is-active")) ?? tabs[0];
    activate(initial.getAttribute("data-detail-tab-target"));
  });
}

function initRevealAnimations() {
  const targets = Array.from(document.querySelectorAll(".metric-card, .chart-card, .bar-row, .mini-bar, .donut-chart, .project-visual-card"));
  if (targets.length === 0) {
    return;
  }

  document.body.classList.add("reveal-ready");

  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches || !("IntersectionObserver" in window)) {
    targets.forEach((target) => target.classList.add("is-visible"));
    return;
  }

  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add("is-visible");
          observer.unobserve(entry.target);
        }
      });
    },
    { threshold: 0.18 }
  );

  targets.forEach((target) => observer.observe(target));
}
