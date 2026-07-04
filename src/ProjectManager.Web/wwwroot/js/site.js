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
  initPasswordToggles();
  initBulkSelection();
  initFilterDrawers();
  initDetailTabs();
  initRichTextEditors();
  initGanttEditors();
  initRevealAnimations();
  initCountUp();
  initCardHoverEffects();
  initUiPageTransitions();
  initClickRipples();
});

function getUiEffectsLevel() {
  if (document.body.classList.contains("ui-effects-low")) {
    return "low";
  }

  if (document.body.classList.contains("ui-effects-high")) {
    return "high";
  }

  return "medium";
}

function initPasswordToggles() {
  document.querySelectorAll("[data-password-toggle]").forEach((button) => {
    const field = button.closest(".password-field");
    const input = field?.querySelector("[data-password-input]");
    const text = button.querySelector("[data-password-toggle-text]");

    if (!input) {
      return;
    }

    button.addEventListener("click", () => {
      const shouldShow = input.type === "password";
      input.type = shouldShow ? "text" : "password";
      button.setAttribute("aria-pressed", shouldShow ? "true" : "false");
      button.setAttribute("aria-label", shouldShow ? "隐藏密码" : "显示密码");

      if (text) {
        text.textContent = shouldShow ? "隐藏" : "显示";
      }
    });
  });
}

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

function initFilterDrawers() {
  document.querySelectorAll("[data-filter-drawer]").forEach((shell) => {
    const panel = shell.querySelector("[data-filter-drawer-panel]");
    const toggle = shell.querySelector("[data-filter-drawer-toggle]");
    const close = shell.querySelector("[data-filter-drawer-close]");

    if (!panel || !toggle) {
      return;
    }

    const hasAdvancedValue = Array.from(panel.querySelectorAll("input, select"))
      .filter((field) => field.type !== "hidden")
      .some((field) => {
        if (field.tagName === "SELECT") {
          if (field.name === "OpenOnly" && field.value === "false") {
            return false;
          }

          return field.value !== "";
        }

        return field.value.trim() !== "";
      });

    shell.classList.add("filter-drawer-enabled");

    const setOpen = (open) => {
      panel.classList.toggle("is-open", open);
      toggle.classList.toggle("is-active", open);
      toggle.setAttribute("aria-expanded", open ? "true" : "false");
    };

    toggle.addEventListener("click", () => {
      setOpen(!panel.classList.contains("is-open"));
    });

    if (close) {
      close.addEventListener("click", () => setOpen(false));
    }

    setOpen(hasAdvancedValue);
  });
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

function initRichTextEditors() {
  document.querySelectorAll("[data-rich-text]").forEach((field) => {
    const source = field.querySelector("[data-rich-text-source]");
    const editor = field.querySelector("[data-rich-text-editor]");
    const form = field.closest("form");

    if (!source || !editor) {
      return;
    }

    const syncSource = () => {
      source.value = editor.innerHTML.trim();
    };

    const focusEditor = () => {
      editor.focus({ preventScroll: true });
    };

    field.querySelectorAll("[data-rich-text-color]").forEach((button) => {
      button.addEventListener("click", () => {
        const color = button.getAttribute("data-rich-text-color");
        if (!color) {
          return;
        }

        focusEditor();
        document.execCommand("styleWithCSS", false, true);
        document.execCommand("foreColor", false, color);
        syncSource();
      });
    });

    const clear = field.querySelector("[data-rich-text-clear]");
    if (clear) {
      clear.addEventListener("click", () => {
        focusEditor();
        document.execCommand("removeFormat", false);
        syncSource();
      });
    }

    editor.addEventListener("input", syncSource);
    editor.addEventListener("blur", syncSource);
    editor.addEventListener("paste", () => {
      window.setTimeout(syncSource, 0);
    });

    if (form) {
      form.addEventListener("submit", syncSource);
    }

    syncSource();
  });
}

function initGanttEditors() {
  document.querySelectorAll("[data-gantt-editor]").forEach((editor) => {
    const rows = editor.querySelector("[data-gantt-rows]");
    const addButton = editor.querySelector("[data-gantt-add-row]");
    if (!rows || !addButton) {
      return;
    }

    const updateRows = () => {
      Array.from(rows.querySelectorAll("[data-gantt-row]")).forEach((row, index) => {
        const number = index + 1;
        const indexLabel = row.querySelector("[data-gantt-index]");
        const sort = row.querySelector("[data-gantt-sort]");

        if (indexLabel) {
          indexLabel.textContent = number.toString();
        }

        if (sort) {
          sort.value = number.toString();
        }

        row.querySelectorAll("input").forEach((input) => {
          input.name = input.name.replace(/GanttInput\.Tasks\[\d+\]/, `GanttInput.Tasks[${index}]`);
        });
      });
    };

    const bindRemove = (row) => {
      const remove = row.querySelector("[data-gantt-remove-row]");
      if (!remove) {
        return;
      }

      remove.addEventListener("click", () => {
        row.remove();
        updateRows();
      });
    };

    rows.querySelectorAll("[data-gantt-row]").forEach(bindRemove);

    addButton.addEventListener("click", () => {
      const template = rows.querySelector("[data-gantt-row]");
      if (!template) {
        return;
      }

      const row = template.cloneNode(true);
      row.querySelectorAll("input").forEach((input) => {
        input.value = "";
        if (input.type === "hidden") {
          input.value = "0";
        }
      });
      rows.appendChild(row);
      bindRemove(row);
      updateRows();
      const firstInput = row.querySelector("input:not([type='hidden'])");
      if (firstInput) {
        firstInput.focus();
      }
    });

    updateRows();
  });
}

function initRevealAnimations() {
  const targets = Array.from(document.querySelectorAll(".metric-card, .chart-card, .bar-row, .mini-bar, .donut-chart, .project-visual-card"));
  if (targets.length === 0) {
    return;
  }

  document.body.classList.add("reveal-ready");

  // 为同一父容器内的兄弟元素设置交错延迟,形成瀑布式淡入
  const groups = new Map();
  targets.forEach((target) => {
    const parent = target.parentElement;
    if (!parent) {
      return;
    }
    const index = groups.get(parent) ?? 0;
    target.style.setProperty("--reveal-index", index.toString());
    target.setAttribute("data-reveal-index", index.toString());
    groups.set(parent, index + 1);
  });

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

function initCountUp() {
  const targets = Array.from(document.querySelectorAll("[data-countup]"));
  if (targets.length === 0) {
    return;
  }

  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  // 将显示文本拆成 前缀 + 数字 + 后缀,只对数字部分做滚动
  const parse = (raw) => {
    const text = (raw ?? "").trim();
    const match = text.match(/-?\d[\d,]*(\.\d+)?/);
    if (!match) {
      return null;
    }
    const numberText = match[0];
    const start = match.index ?? 0;
    return {
      prefix: text.slice(0, start),
      suffix: text.slice(start + numberText.length),
      value: parseFloat(numberText.replace(/,/g, "")),
      decimals: numberText.includes(".") ? numberText.split(".")[1].length : 0,
      grouped: numberText.includes(","),
    };
  };

  const format = (value, info) => {
    const fixed = value.toFixed(info.decimals);
    if (!info.grouped) {
      return fixed;
    }
    const [intPart, decPart] = fixed.split(".");
    const grouped = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, ",");
    return decPart ? `${grouped}.${decPart}` : grouped;
  };

  const animate = (el, info) => {
    if (reduceMotion || info.value === 0) {
      el.textContent = `${info.prefix}${format(info.value, info)}${info.suffix}`;
      return;
    }

    const duration = 1100;
    let startTime = null;

    const step = (timestamp) => {
      if (startTime === null) {
        startTime = timestamp;
      }
      const progress = Math.min((timestamp - startTime) / duration, 1);
      // easeOutCubic
      const eased = 1 - Math.pow(1 - progress, 3);
      const current = info.value * eased;
      el.textContent = `${info.prefix}${format(current, info)}${info.suffix}`;
      if (progress < 1) {
        window.requestAnimationFrame(step);
      } else {
        el.textContent = `${info.prefix}${format(info.value, info)}${info.suffix}`;
      }
    };

    window.requestAnimationFrame(step);
  };

  const run = (el) => {
    if (el.dataset.countupDone === "true") {
      return;
    }
    const info = parse(el.textContent);
    if (!info) {
      return;
    }
    el.dataset.countupDone = "true";
    animate(el, info);
  };

  if (reduceMotion || !("IntersectionObserver" in window)) {
    targets.forEach(run);
    return;
  }

  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          run(entry.target);
          observer.unobserve(entry.target);
        }
      });
    },
    { threshold: 0.35 }
  );

  targets.forEach((target) => observer.observe(target));
}

function initCardHoverEffects() {
  const hoverSelector = [
    ".metric-card",
    ".chart-card",
    ".module-card",
    ".entrance-panel",
    ".project-visual-card",
    ".account-panel"
  ].join(",");

  const level = getUiEffectsLevel();
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const canHover = window.matchMedia("(hover: hover)").matches;

  document.querySelectorAll(hoverSelector).forEach((card) => {
    if ((level === "medium" || level === "high") && !reduceMotion && canHover) {
      card.classList.remove("hover-card");
      card.classList.add("tilt-card");
      if (!card.querySelector(":scope > .tilt-glare")) {
        const glare = document.createElement("span");
        glare.className = "tilt-glare";
        glare.setAttribute("aria-hidden", "true");
        card.appendChild(glare);
      }

      let frame = 0;
      let pointerEvent = null;
      const updateTilt = () => {
        if (!pointerEvent) {
          frame = 0;
          return;
        }

        const rect = card.getBoundingClientRect();
        const x = (pointerEvent.clientX - rect.left) / rect.width;
        const y = (pointerEvent.clientY - rect.top) / rect.height;
        card.style.setProperty("--tilt-x", `${(0.5 - y) * 7}deg`);
        card.style.setProperty("--tilt-y", `${(x - 0.5) * 8}deg`);
        card.style.setProperty("--tilt-lift", "-4px");
        card.style.setProperty("--tilt-scale", "1.012");
        card.style.setProperty("--glare-x", `${x * 100}%`);
        card.style.setProperty("--glare-y", `${y * 100}%`);
        card.classList.add("is-tilting");
        frame = 0;
      };

      card.addEventListener("pointermove", (event) => {
        pointerEvent = event;
        if (frame === 0) {
          frame = window.requestAnimationFrame(updateTilt);
        }
      });

      card.addEventListener("pointerleave", () => {
        pointerEvent = null;
        if (frame !== 0) {
          window.cancelAnimationFrame(frame);
          frame = 0;
        }
        card.classList.remove("is-tilting");
        card.style.removeProperty("--tilt-x");
        card.style.removeProperty("--tilt-y");
        card.style.removeProperty("--tilt-lift");
        card.style.removeProperty("--tilt-scale");
        card.style.removeProperty("--glare-x");
        card.style.removeProperty("--glare-y");
      });

      return;
    }

    card.classList.add("hover-card");
    card.classList.remove("tilt-card", "is-tilting");
    card.style.removeProperty("--tilt-x");
    card.style.removeProperty("--tilt-y");
    card.style.removeProperty("--tilt-lift");
    card.style.removeProperty("--tilt-scale");
    card.style.removeProperty("--glare-x");
    card.style.removeProperty("--glare-y");
  });
}

function initUiPageTransitions() {
  const level = getUiEffectsLevel();
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  if (level !== "high" || reduceMotion) {
    return;
  }

  const overlay = document.createElement("div");
  overlay.className = "page-transition-overlay";
  overlay.setAttribute("data-ui-page-transition", "true");
  overlay.setAttribute("aria-hidden", "true");
  document.body.appendChild(overlay);
  window.requestAnimationFrame(() => document.body.classList.add("page-transition-ready"));

  document.addEventListener("click", (event) => {
    const link = event.target.closest?.("a[href]");
    if (!link || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return;
    }

    const href = link.getAttribute("href");
    const target = link.getAttribute("target");
    if (!href || target === "_blank" || href.startsWith("#") || href.startsWith("javascript:")) {
      return;
    }

    const destination = new URL(href, window.location.href);
    if (destination.origin !== window.location.origin || destination.href === window.location.href) {
      return;
    }

    event.preventDefault();
    document.body.classList.add("page-transition-leaving");
    window.setTimeout(() => {
      window.location.href = destination.href;
    }, 170);
  });
}

function initClickRipples() {
  const level = getUiEffectsLevel();
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  if (level !== "high" || reduceMotion) {
    return;
  }

  const selector = ".btn, .nav-link, .page-link, .ui-effects-level-card, .module-card a";
  document.addEventListener("pointerdown", (event) => {
    const target = event.target.closest?.(selector);
    if (!target || target.disabled) {
      return;
    }

    const rect = target.getBoundingClientRect();
    const size = Math.max(rect.width, rect.height);
    const ripple = document.createElement("span");
    ripple.className = "ui-click-ripple";
    ripple.style.width = `${size}px`;
    ripple.style.height = `${size}px`;
    ripple.style.left = `${event.clientX - rect.left - size / 2}px`;
    ripple.style.top = `${event.clientY - rect.top - size / 2}px`;
    ripple.setAttribute("aria-hidden", "true");

    target.classList.add("ui-ripple-host");
    target.appendChild(ripple);
    ripple.addEventListener("animationend", () => ripple.remove(), { once: true });
  });
}
