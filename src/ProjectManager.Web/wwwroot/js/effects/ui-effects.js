/* Optional motion and visual effects. */

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

export function initUiEffects() {
  initApplePressFeedback();
  initRevealAnimations();
  initCountUp();
  initCardHoverEffects();
  initUiPageTransitions();
  initClickRipples();
}
