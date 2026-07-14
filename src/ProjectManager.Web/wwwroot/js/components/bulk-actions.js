/* Bulk row selection and submission. */

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

export function initBulkActions() {
  initBulkSelection();
}
