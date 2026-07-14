/* Server-backed personal data views. */

function currentFilters(bar) {
  const form = document.querySelector("[data-saved-view-filters]");
  if (!form) {
    try {
      return JSON.parse(bar.getAttribute("data-current-filters") || "{}") || {};
    } catch {
      return {};
    }
  }

  const ignored = new Set(["PageNumber", "PageSize", "ViewPreset", "SavedViewId"]);
  const filters = {};
  new FormData(form).forEach((value, key) => {
    if (!ignored.has(key) && String(value).trim() !== "") {
      filters[key] = String(value);
    }
  });
  return filters;
}

function dataTableState() {
  const table = document.querySelector("[data-column-manager-table]");
  if (!table) {
    return { visibleColumns: [], rowDensity: "Normal" };
  }

  let visibleColumns = [];
  try {
    visibleColumns = JSON.parse(table.getAttribute("data-current-visible-columns") || "[]") || [];
  } catch {
    visibleColumns = [];
  }

  return {
    visibleColumns,
    rowDensity: table.getAttribute("data-current-row-density") || "Normal"
  };
}

function setProcessing(form) {
  const button = form.querySelector("button[type='submit']");
  if (!button || button.disabled) {
    return;
  }
  button.disabled = true;
  button.setAttribute("aria-busy", "true");
  button.textContent = button.getAttribute("data-processing-label") || "正在處理…";
}

function confirmAction(message) {
  return new Promise((resolve) => {
    const dialog = document.createElement("dialog");
    dialog.className = "app-confirm-dialog";
    dialog.innerHTML = `
      <form method="dialog" class="app-confirm-dialog-card">
        <p></p>
        <div class="app-confirm-dialog-actions">
          <button value="cancel" class="btn btn-outline-secondary">取消</button>
          <button value="confirm" class="btn btn-danger">確定刪除</button>
        </div>
      </form>`;
    dialog.querySelector("p").textContent = message;
    dialog.addEventListener("close", () => {
      const confirmed = dialog.returnValue === "confirm";
      dialog.remove();
      resolve(confirmed);
    }, { once: true });
    document.body.appendChild(dialog);
    dialog.showModal();
  });
}

export function initSavedViews() {
  document.querySelectorAll("[data-saved-view-bar]").forEach((bar) => {
    const picker = bar.querySelector("[data-saved-view-picker] select");
    picker?.addEventListener("change", () => picker.form?.requestSubmit());

    const saveForm = bar.querySelector("[data-saved-view-save]");
    saveForm?.addEventListener("submit", () => {
      const state = dataTableState();
      saveForm.querySelector("[data-saved-view-filter-json]").value = JSON.stringify(currentFilters(bar));
      saveForm.querySelector("[data-saved-view-column-json]").value = JSON.stringify(state.visibleColumns);
      saveForm.querySelector("[data-saved-view-density]").value = state.rowDensity;
      setProcessing(saveForm);
    });

    bar.querySelectorAll("form:not([data-saved-view-save])").forEach((form) => {
      form.addEventListener("submit", async (event) => {
        const message = form.getAttribute("data-confirm-message");
        if (message && form.getAttribute("data-confirmed") !== "true") {
          event.preventDefault();
          if (await confirmAction(message)) {
            form.setAttribute("data-confirmed", "true");
            form.requestSubmit();
          }
          return;
        }
        setProcessing(form);
      });
    });
  });
}
