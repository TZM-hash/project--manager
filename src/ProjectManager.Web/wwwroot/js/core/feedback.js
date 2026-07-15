/* Shared field and form feedback helpers. */

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

function initProcessingFeedback() {
  const restore = () => {
    document.querySelectorAll("[data-processing-active]").forEach((control) => {
      control.removeAttribute("data-processing-active");
      control.removeAttribute("aria-busy");
      control.removeAttribute("aria-disabled");
      if (control.matches("button, input")) {
        control.disabled = false;
      }
      if (control.hasAttribute("data-original-label")) {
        control.textContent = control.getAttribute("data-original-label");
      }
    });
  };

  document.querySelectorAll("form:has([data-processing-label])").forEach((form) => {
    form.addEventListener("submit", (event) => {
      setTimeout(() => {
        if (event.defaultPrevented || !form.checkValidity()) {
          return;
        }
        const button = form.querySelector("[data-processing-label]");
        if (!button) {
          return;
        }
        button.setAttribute("data-original-label", button.textContent.trim());
        button.setAttribute("data-processing-active", "true");
        button.setAttribute("aria-busy", "true");
        button.textContent = button.getAttribute("data-processing-label") || "正在處理…";
        button.disabled = true;
      }, 0);
    });
  });

  document.querySelectorAll("[data-processing-link]").forEach((link) => {
    link.addEventListener("click", () => {
      link.setAttribute("data-original-label", link.textContent.trim());
      link.setAttribute("data-processing-active", "true");
      link.setAttribute("aria-busy", "true");
      link.setAttribute("aria-disabled", "true");
      link.textContent = link.getAttribute("data-processing-label") || "正在準備…";
    });
  });

  window.addEventListener("pageshow", restore);
}

function initValidationFeedback() {
  const summary = document.querySelector(".validation-summary-errors");
  if (!summary || !summary.textContent.trim()) {
    return;
  }

  summary.setAttribute("role", "alert");
  summary.setAttribute("tabindex", "-1");
  window.requestAnimationFrame(() => {
    summary.scrollIntoView({ behavior: "smooth", block: "center" });
    const invalidField = document.querySelector(".input-validation-error, [aria-invalid='true']");
    if (invalidField instanceof HTMLElement) {
      invalidField.focus({ preventScroll: true });
      return;
    }
    summary.focus({ preventScroll: true });
  });
}

export function initFeedback() {
  initPasswordToggles();
  initProcessingFeedback();
  initValidationFeedback();
}
