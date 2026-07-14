/* Rich-text color editor. */

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

export function initRichText() {
  initRichTextEditors();
}
