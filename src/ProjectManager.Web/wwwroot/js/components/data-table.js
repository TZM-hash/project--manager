/* Column visibility and row-density controls. */

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

    let initialVisibleColumns = null;
    try {
      const initial = table.getAttribute("data-initial-visible-columns");
      initialVisibleColumns = initial ? JSON.parse(initial) : null;
    } catch (e) {
      initialVisibleColumns = null;
    }

    const headers = Array.from(table.querySelectorAll("thead th[data-column]:not([data-column-fixed])"));
    const columns = headers.map((th) => {
      const key = th.getAttribute("data-column");
      const label = th.getAttribute("data-column-label") || th.textContent.trim() || key;
      const order = Number(th.getAttribute("data-column-order") || Number.MAX_SAFE_INTEGER);
      return { key, label, order, th };
    }).sort((left, right) => left.order - right.order);

    if (list) {
      list.innerHTML = "";
      columns.forEach((col) => {
        const isVisible = Array.isArray(initialVisibleColumns)
          ? initialVisibleColumns.includes(col.key)
          : saved[col.key] !== false;
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
      const configurableCheckboxes = Array.from(list?.querySelectorAll("[data-column-key]") || []);
      if (configurableCheckboxes.length > 0 && !configurableCheckboxes.some((checkbox) => checkbox.checked)) {
        configurableCheckboxes[0].checked = true;
      }
      columns.forEach((col) => {
        const checkbox = list?.querySelector(`[data-column-key="${col.key}"]`);
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

      table.querySelectorAll("thead th[data-column-group]").forEach((groupHeader) => {
        const groupKeys = (groupHeader.getAttribute("data-column-group") || "")
          .split(/\s+/)
          .filter(Boolean);
        const visibleCount = groupKeys.filter((key) => newState[key] !== false).length;
        groupHeader.style.display = visibleCount > 0 ? "" : "none";
        if (visibleCount > 0) {
          groupHeader.colSpan = visibleCount;
        }
      });

      table.setAttribute(
        "data-visible-column-count",
        String(columns.filter((col) => newState[col.key] !== false).length)
      );
      const fixedColumns = Array.from(table.querySelectorAll("thead th[data-column][data-column-fixed]"))
        .map((header) => header.getAttribute("data-column"))
        .filter(Boolean);
      const visibleColumns = [
        ...fixedColumns,
        ...columns.filter((col) => newState[col.key] !== false).map((col) => col.key)
      ];
      table.setAttribute("data-current-visible-columns", JSON.stringify([...new Set(visibleColumns)]));
      table.dispatchEvent(new CustomEvent("data-table-state-change", { bubbles: true }));
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
    const table = manager.closest(".data-list-card, .data-work-surface")?.querySelector("[data-column-manager-table]") ??
      document.querySelector("[data-column-manager-table]");
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

    const serverDensity = (table.getAttribute("data-initial-row-density") || "").toLowerCase();
    let saved = serverDensity || localStorage.getItem(storageKey) || "normal";

    const applySpacing = (mode) => {
      const styles = spacingStyles[mode] || spacingStyles.normal;
      table.style.setProperty("--row-padding", styles.padding);
      table.style.setProperty("--row-font-size", styles.fontSize);
      table.style.setProperty("--row-line-height", styles.lineHeight);
      table.classList.remove("row-spacing-compact", "row-spacing-spacious");
      if (mode !== "normal") {
        table.classList.add(`row-spacing-${mode}`);
      }
      const persistedName = mode.charAt(0).toUpperCase() + mode.slice(1);
      table.setAttribute("data-current-row-density", persistedName);
      table.dispatchEvent(new CustomEvent("data-table-state-change", { bubbles: true }));
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

export function initDataTables() {
  initColumnManagers();
  initRowSpacing();
}
