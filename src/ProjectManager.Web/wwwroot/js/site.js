import { initShell } from "./core/shell.js";

initShell();

const jobs = [];

if (document.querySelector("[data-password-toggle], [data-processing-label], [data-processing-link]")) {
  jobs.push(import("./core/feedback.js").then((module) => module.initFeedback()));
}
if (document.querySelector("[data-bulk-form], [data-bulk-checkbox]")) {
  jobs.push(import("./components/bulk-actions.js").then((module) => module.initBulkActions()));
}
if (document.querySelector("[data-filter-drawer]")) {
  jobs.push(import("./components/filter-drawer.js").then((module) => module.initFilterDrawer()));
}
if (document.querySelector("[data-detail-tabs]")) {
  jobs.push(import("./components/detail-tabs.js").then((module) => module.initDetailsTabs()));
}
if (document.querySelector("[data-rich-text]")) {
  jobs.push(import("./components/rich-text.js").then((module) => module.initRichText()));
}
if (document.querySelector("[data-gantt-editor]")) {
  jobs.push(import("./components/gantt-editor.js").then((module) => module.initGanttEditor()));
}
if (document.querySelector("[data-column-manager], [data-row-spacing]")) {
  jobs.push(import("./components/data-table.js").then((module) => module.initDataTables()));
}
if (document.querySelector("[data-saved-view-bar]")) {
  jobs.push(import("./components/saved-views.js").then((module) => module.initSavedViews()));
}
if (document.querySelector("[data-theme-option], [data-motion-option], [data-global-font-picker]")) {
  jobs.push(import("./pages/settings.js").then((module) => module.initSettings()));
}
if (document.body.matches(".ui-effects-medium, .ui-effects-high, .motion-apple")) {
  jobs.push(import("./effects/ui-effects.js").then((module) => module.initUiEffects()));
}

await Promise.all(jobs);
