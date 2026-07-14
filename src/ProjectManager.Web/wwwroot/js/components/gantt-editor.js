/* Editable Gantt task rows. */

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

        row.querySelectorAll("input, select, textarea").forEach((field) => {
          field.name = field.name.replace(/GanttInput\.Tasks\[\d+\]/, `GanttInput.Tasks[${index}]`);
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
      row.querySelectorAll("input, textarea").forEach((input) => {
        if (input.type === "checkbox") {
          input.checked = false;
        } else {
          input.value = "";
        }
        if (input.type === "hidden" && input.name.endsWith(".Id")) {
          input.value = "0";
        }
      });
      row.querySelectorAll("select").forEach((select) => {
        select.selectedIndex = 0;
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

export function initGanttEditor() {
  initGanttEditors();
}
