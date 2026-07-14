/* Advanced filter drawer behavior. */

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

export function initFilterDrawer() {
  initFilterDrawers();
}
