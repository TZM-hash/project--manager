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
  initThemePreview();
  initMotionStylePreview();
  initGlobalFontPreview();
  initApplePressFeedback();
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
  initColumnManagers();
  initRowSpacing();
});

function initThemePreview() {
  const options = document.querySelectorAll("[data-theme-option]");
  if (options.length === 0) {
    return;
  }

  options.forEach((option) => {
    const input = option.querySelector('input[type="radio"]');
    if (!input) {
      return;
    }

    input.addEventListener("change", () => {
      if (!input.checked) {
        return;
      }

      document.body.classList.remove("theme-default", "theme-clear-glass");
      document.body.classList.add(option.dataset.themeOption || "theme-default");
    });
  });
}

function initMotionStylePreview() {
  const options = document.querySelectorAll("[data-motion-option]");
  if (options.length === 0) {
    return;
  }

  options.forEach((option) => {
    const input = option.querySelector('input[type="radio"]');
    if (!input) {
      return;
    }

    input.addEventListener("change", () => {
      if (!input.checked) {
        return;
      }

      document.body.classList.remove("motion-default", "motion-apple");
      document.body.classList.add(option.dataset.motionOption || "motion-default");
    });
  });
}

function initGlobalFontPreview() {
  const picker = document.querySelector("[data-global-font-picker]");
  const select = picker?.querySelector("[data-global-font-select]");
  const preview = picker?.querySelector("[data-global-font-preview]");
  if (!picker || !select || !preview) {
    return;
  }

  const fontClasses = [
    "font-system-default",
    "font-microsoft-yahei",
    "font-microsoft-jhenghei",
    "font-chinese-serif",
    "font-chinese-kai"
  ];
  const previewClasses = [
    "font-preview-system",
    "font-preview-yahei",
    "font-preview-jhenghei",
    "font-preview-serif",
    "font-preview-kai"
  ];

  const applyPreview = () => {
    const option = select.options[select.selectedIndex];
    if (!option) {
      return;
    }

    document.body.classList.remove(...fontClasses);
    document.body.classList.add(option.dataset.fontClass || "font-system-default");
    select.classList.remove(...previewClasses);
    preview.classList.remove(...previewClasses);
    const previewClass = option.dataset.previewClass || "font-preview-system";
    select.classList.add(previewClass);
    preview.classList.add(previewClass);

    const name = preview.querySelector("[data-global-font-name]");
    const description = preview.querySelector("[data-global-font-description]");
    if (name) {
      name.textContent = option.dataset.fontName || option.textContent;
    }
    if (description) {
      description.textContent = option.dataset.fontDescription || "";
    }
  };

  select.addEventListener("change", applyPreview);
  applyPreview();
}

function initApplePressFeedback() {
  const selector = ".btn, .nav-link, .page-link, .detail-tab, .theme-option-card, .motion-style-card, .ui-effects-level-card, .module-card a";
  const release = (target) => target?.classList.remove("is-apple-pressed");

  document.addEventListener("pointerdown", (event) => {
    if (!document.body.classList.contains("motion-apple")) {
      return;
    }

    const target = event.target.closest?.(selector);
    if (!target || target.disabled) {
      return;
    }

    target.classList.add("is-apple-pressed");
  });

  document.addEventListener("pointerup", (event) => release(event.target.closest?.(selector)));
  document.addEventListener("pointercancel", (event) => release(event.target.closest?.(selector)));
  document.addEventListener("pointerout", (event) => release(event.target.closest?.(selector)));
}

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
    const counter = field.querySelector("#progress-description-counter");
    const countSpan = field.querySelector("#progress-description-count");
    const maxLength = 1000;

    if (!source || !editor) {
      return;
    }

    const syncSource = () => {
      source.value = editor.innerHTML.trim();
      updateCounter();
    };

    const updateCounter = () => {
      if (!countSpan || !counter) {
        return;
      }
      const text = source.value || "";
      const length = text.length;
      countSpan.textContent = length.toString();
      if (length > maxLength) {
        counter.classList.remove("text-muted");
        counter.classList.add("text-danger", "font-weight-bold");
      } else {
        counter.classList.remove("text-danger", "font-weight-bold");
        counter.classList.add("text-muted");
      }
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
      form.addEventListener("submit", (event) => {
        const text = source.value || "";
        if (text.length > maxLength) {
          event.preventDefault();
          window.alert(`狀態說明字數(${text.length})已超過最大限制(${maxLength}個字符)，請刪除部分內容後再儲存。`);
          editor.focus();
          return;
        }
        syncSource();
      });
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
  const appleMotion = document.body.classList.contains("motion-apple");
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const canHover = window.matchMedia("(hover: hover)").matches;

  document.querySelectorAll(hoverSelector).forEach((card) => {
    if ((level === "medium" || level === "high") && !appleMotion && !reduceMotion && canHover) {
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
  window.addEventListener("pageshow", () => {
    document.body.classList.remove("page-transition-leaving");
    document.body.removeAttribute("aria-busy");
  });

  document.addEventListener("click", (event) => {
    const link = event.target.closest?.("a[href]");
    if (!link || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return;
    }

    const href = link.getAttribute("href");
    const target = link.getAttribute("target");
    if (!href
        || (target && target !== "_self")
        || link.hasAttribute("download")
        || link.hasAttribute("data-no-transition")
        || href.startsWith("#")
        || href.startsWith("javascript:")) {
      return;
    }

    const destination = new URL(href, window.location.href);
    if (destination.origin !== window.location.origin || destination.href === window.location.href) {
      return;
    }

    if (document.body.classList.contains("page-transition-leaving")) {
      event.preventDefault();
      return;
    }

    const appleMotion = document.body.classList.contains("motion-apple");
    const navigationDelay = appleMotion ? 45 : 30;
    event.preventDefault();
    document.body.setAttribute("aria-busy", "true");
    document.body.classList.add("page-transition-leaving");
    window.setTimeout(() => {
      window.location.assign(destination.href);
    }, navigationDelay);
  });
}

function initClickRipples() {
  const level = getUiEffectsLevel();
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const appleMotion = document.body.classList.contains("motion-apple");
  if (level !== "high" || reduceMotion || appleMotion) {
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

function initColumnManagers() {
  document.querySelectorAll("[data-column-manager]").forEach((manager) => {
    const table = manager.nextElementSibling?.querySelector("[data-column-manager-table]") ??
                  manager.parentElement?.nextElementSibling?.querySelector("[data-column-manager-table]") ??
                  document.querySelector("[data-column-manager-table]");
    if (!table) {
      return;
    }

    const toggle = manager.querySelector("[data-column-manager-toggle]");
    const list = manager.querySelector("[data-column-manager-list]");
    const allBtn = manager.querySelector("[data-column-manager-all]");
    const noneBtn = manager.querySelector("[data-column-manager-none]");
    const storageKey = `column-manager-${table.id || manager.parentElement?.closest(".data-list-card")?.getAttribute("data-tab") || "default"}`;

    let saved = {};
    try {
      saved = JSON.parse(localStorage.getItem(storageKey) || "{}") || {};
    } catch (e) {
      saved = {};
    }

    const headers = Array.from(table.querySelectorAll("thead th[data-column]"));
    const columns = headers.map((th) => {
      const key = th.getAttribute("data-column");
      const label = th.textContent.trim() || key;
      return { key, label, th };
    });

    if (list) {
      list.innerHTML = "";
      columns.forEach((col) => {
        const isVisible = saved[col.key] !== false;
        const item = document.createElement("div");
        item.className = "form-check";
        item.innerHTML = `
          <input class="form-check-input" type="checkbox" data-column-key="${col.key}" id="col-${col.key}" ${isVisible ? "checked" : ""}>
          <label class="form-check-label" for="col-${col.key}">${col.label}</label>
        `;
        list.appendChild(item);
      });
    }

    const applyColumns = () => {
      const newState = {};
      columns.forEach((col) => {
        const checkbox = list.querySelector(`[data-column-key="${col.key}"]`);
        const visible = checkbox ? checkbox.checked : true;
        newState[col.key] = visible;
        const display = visible ? "" : "none";
        col.th.style.display = display;
        table.querySelectorAll(`tbody td[data-column="${col.key}"]`).forEach((td) => {
          td.style.display = display;
        });
        table.querySelectorAll(`tfoot td[data-column="${col.key}"]`).forEach((td) => {
          td.style.display = display;
        });
      });
      try {
        localStorage.setItem(storageKey, JSON.stringify(newState));
      } catch (e) {
        // ignore
      }
    };

    if (list) {
      list.addEventListener("change", (event) => {
        const target = event.target;
        if (target.matches("[data-column-key]")) {
          applyColumns();
        }
      });
    }

    if (allBtn) {
      allBtn.addEventListener("click", (event) => {
        event.preventDefault();
        event.stopPropagation();
        list.querySelectorAll("[data-column-key]").forEach((cb) => {
          cb.checked = true;
        });
        applyColumns();
      });
    }

    if (noneBtn) {
      noneBtn.addEventListener("click", (event) => {
        event.preventDefault();
        event.stopPropagation();
        list.querySelectorAll("[data-column-key]").forEach((cb) => {
          cb.checked = false;
        });
        applyColumns();
      });
    }

    if (toggle) {
      toggle.addEventListener("click", (event) => {
        event.preventDefault();
        event.stopPropagation();
        const menu = manager.querySelector(".dropdown-menu");
        if (menu) {
          menu.classList.toggle("show");
        }
      });
    }

    applyColumns();
  });

  document.addEventListener("click", (event) => {
    document.querySelectorAll("[data-column-manager] .dropdown-menu.show").forEach((menu) => {
      if (!menu.parentElement.contains(event.target)) {
        menu.classList.remove("show");
      }
    });
  });
}

function initRowSpacing() {
  document.querySelectorAll("[data-row-spacing]").forEach((manager) => {
    const table = document.querySelector("[data-column-manager-table]");
    if (!table) {
      return;
    }

    const toggle = manager.querySelector("[data-row-spacing-toggle]");
    const storageKey = `row-spacing-${table.id || "default"}`;

    const spacingStyles = {
      compact: { padding: "0.125rem 0.375rem", fontSize: "0.7rem", lineHeight: "1.1" },
      normal: { padding: "0.5rem 0.5rem", fontSize: "0.875rem", lineHeight: "1.5" },
      spacious: { padding: "0.75rem 0.5rem", fontSize: "0.9rem", lineHeight: "1.6" }
    };

    let saved = localStorage.getItem(storageKey) || "normal";

    const applySpacing = (mode) => {
      const styles = spacingStyles[mode] || spacingStyles.normal;
      table.style.setProperty("--row-padding", styles.padding);
      table.style.setProperty("--row-font-size", styles.fontSize);
      table.style.setProperty("--row-line-height", styles.lineHeight);
      table.classList.remove("row-spacing-compact", "row-spacing-spacious");
      if (mode !== "normal") {
        table.classList.add(`row-spacing-${mode}`);
      }
      localStorage.setItem(storageKey, mode);
    };

    applySpacing(saved);

    manager.querySelectorAll("[data-row-spacing-option]").forEach((option) => {
      option.addEventListener("click", (event) => {
        event.preventDefault();
        const mode = option.getAttribute("data-row-spacing-option");
        if (mode) {
          applySpacing(mode);
        }
        const menu = manager.querySelector(".dropdown-menu");
        if (menu) {
          menu.classList.remove("show");
        }
      });
    });

    if (toggle) {
      toggle.addEventListener("click", (event) => {
        event.preventDefault();
        event.stopPropagation();
        const menu = manager.querySelector(".dropdown-menu");
        if (menu) {
          menu.classList.toggle("show");
        }
      });
    }
  });

  document.addEventListener("click", (event) => {
    document.querySelectorAll("[data-row-spacing] .dropdown-menu.show").forEach((menu) => {
      if (!menu.parentElement.contains(event.target)) {
        menu.classList.remove("show");
      }
    });
  });
}
