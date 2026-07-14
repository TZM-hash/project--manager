/* System setting live previews. */

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

export function initSettings() {
  initThemePreview();
  initMotionStylePreview();
  initGlobalFontPreview();
}
