/* Shared application shell and active navigation. */

function setSidebarCollapsed(collapsed) {
  document.body.classList.toggle("sidebar-collapsed", collapsed);
  const toggle = document.querySelector("[data-sidebar-toggle]");
  if (toggle) {
    toggle.setAttribute("aria-expanded", collapsed ? "false" : "true");
    toggle.setAttribute("aria-label", collapsed ? "展開側邊導覽" : "收合側邊導覽");
  }
}

window.toggleSidebar = function () {
  setSidebarCollapsed(!document.body.classList.contains("sidebar-collapsed"));
};

window.toggleNavGroup = function (el) {
  const item = el.closest(".nav-group");
  if (item) {
    item.classList.toggle("nav-group-open");
  }
};

function initActiveNavigation() {
  const currentPath = window.location.pathname.replace(/\/$/, "") || "/";
  const links = Array.from(document.querySelectorAll(".app-sidebar-nav a.nav-link[href]"));
  const candidates = links
    .map((link) => {
      const href = link.getAttribute("href");
      if (!href || href.startsWith("javascript:") || href.startsWith("#")) {
        return null;
      }

      const linkPath = new URL(href, window.location.origin).pathname.replace(/\/$/, "") || "/";
      const matches = linkPath === "/"
        ? currentPath === "/"
        : currentPath === linkPath || currentPath.startsWith(`${linkPath}/`);
      return matches ? { link, linkPath } : null;
    })
    .filter(Boolean)
    .sort((left, right) => right.linkPath.length - left.linkPath.length);

  const active = candidates[0]?.link;
  if (!active) {
    return;
  }

  active.classList.add("active");
  active.setAttribute("aria-current", "page");
  active.closest(".nav-group")?.classList.add("nav-group-open");
}

function initSidebarToggle() {
  const toggle = document.querySelector("[data-sidebar-toggle]");
  if (!toggle) {
    return;
  }

  setSidebarCollapsed(document.body.classList.contains("sidebar-collapsed"));
  toggle.addEventListener("click", window.toggleSidebar);
}

function initCollapsedNavigationTooltips() {
  const links = Array.from(document.querySelectorAll(".app-sidebar-nav [data-nav-label]"));
  const closeAll = () => links.forEach((link) => link.removeAttribute("data-tooltip-visible"));

  links.forEach((link) => {
    const open = () => {
      if (document.body.classList.contains("sidebar-collapsed") || window.matchMedia("(max-width: 767.98px)").matches) {
        closeAll();
        link.setAttribute("data-tooltip-visible", "true");
      }
    };

    link.addEventListener("focus", open);
    link.addEventListener("mouseenter", open);
    link.addEventListener("blur", () => link.removeAttribute("data-tooltip-visible"));
    link.addEventListener("mouseleave", () => link.removeAttribute("data-tooltip-visible"));
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      closeAll();
    }
  });
}

export function initShell() {
  initActiveNavigation();
  initSidebarToggle();
  initCollapsedNavigationTooltips();
}
