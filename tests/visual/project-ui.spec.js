const { test, expect } = require("@playwright/test");
const fs = require("fs");
const path = require("path");

const baseURL = process.env.BASE_URL || "http://localhost:62383";
const userName = process.env.VISUAL_USER || "admin";
const password = process.env.VISUAL_PASSWORD || "ChangeMe123!";
const outputDir =
  process.env.VISUAL_OUTPUT_DIR || path.join(process.cwd(), "..", "..", "artifacts", "visual");
const browserExecutablePath = process.env.PLAYWRIGHT_EXECUTABLE_PATH;

test.use({
  baseURL,
  viewport: { width: 1440, height: 900 },
  launchOptions: browserExecutablePath ? { executablePath: browserExecutablePath } : undefined
});

test.setTimeout(60000);

async function signIn(page) {
  await page.goto("/Identity/Account/Login");
  await page.locator("#Input_UserName").fill(userName);
  await page.locator("#Input_Password").fill(password);
  await page.locator('button[type="submit"]').click();

  await expect(page.locator('form[action*="/Account/Logout"]')).toBeVisible();
}

async function capture(page, name) {
  fs.mkdirSync(outputDir, { recursive: true });
  await page.screenshot({
    path: path.join(outputDir, `${name}.png`),
    fullPage: true
  });
}

async function captureElement(page, selector, name) {
  fs.mkdirSync(outputDir, { recursive: true });
  await page.locator(selector).screenshot({ path: path.join(outputDir, `${name}.png`) });
}

async function expectPaginationUsable(page) {
  const pagination = page.locator(".pagination-wrap").first();
  await expect(pagination).toBeVisible();
  await pagination.scrollIntoViewIfNeeded();

  const metrics = await page.evaluate(() => {
    const paginationElement = document.querySelector(".pagination-wrap");
    const footerElement = document.querySelector(".app-footer");
    const selectElement = document.querySelector(".pagination-size-form select");

    if (!paginationElement || !footerElement || !selectElement) {
      return {
        paginationBeforeFooter: false,
        selectClickable: false
      };
    }

    const paginationRect = paginationElement.getBoundingClientRect();
    const footerRect = footerElement.getBoundingClientRect();
    const selectRect = selectElement.getBoundingClientRect();
    const centerX = selectRect.left + selectRect.width / 2;
    const centerY = selectRect.top + selectRect.height / 2;
    const hitElement = document.elementFromPoint(centerX, centerY);

    return {
      paginationBeforeFooter: paginationRect.bottom <= footerRect.top,
      selectClickable: hitElement === selectElement || selectElement.contains(hitElement)
    };
  });

  expect(metrics.paginationBeforeFooter).toBeTruthy();
  expect(metrics.selectClickable).toBeTruthy();
}

function relativeLuminance([red, green, blue]) {
  const channels = [red, green, blue].map((value) => {
    const channel = value / 255;
    return channel <= 0.03928 ? channel / 12.92 : Math.pow((channel + 0.055) / 1.055, 2.4);
  });
  return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
}

function contrastRatio(foreground, background) {
  const lighter = Math.max(relativeLuminance(foreground), relativeLuminance(background));
  const darker = Math.min(relativeLuminance(foreground), relativeLuminance(background));
  return (lighter + 0.05) / (darker + 0.05);
}

test.beforeEach(async ({ page }) => {
  await signIn(page);
});

test("personal workbench keeps one primary action keyboard accessible at 200 percent zoom", async ({ page }) => {
  await page.setViewportSize({ width: 720, height: 900 });
  await page.goto("/");

  const hero = page.locator(".workbench-hero");
  const primaryAction = page.locator(".workbench-primary-action");
  await expect(hero).toBeVisible();
  await expect(primaryAction).toHaveCount(1);
  await expect(page.locator(".workbench-counter")).toHaveCount(4);
  await primaryAction.focus();
  await expect(primaryAction).toBeFocused();

  const layout = await page.evaluate(() => {
    const action = document.querySelector(".workbench-primary-action");
    const style = action ? getComputedStyle(action) : null;
    const parseRgb = (value) => (value.match(/\d+(?:\.\d+)?/g) || []).slice(0, 3).map(Number);
    return {
      noPageOverflow: document.documentElement.scrollWidth <= window.innerWidth,
      actionWidth: action?.getBoundingClientRect().width || 0,
      viewportWidth: window.innerWidth,
      foreground: style ? parseRgb(style.color) : [],
      background: style ? parseRgb(style.backgroundColor) : []
    };
  });

  expect(layout.noPageOverflow).toBeTruthy();
  expect(layout.actionWidth).toBeLessThanOrEqual(layout.viewportWidth);
  expect(contrastRatio(layout.foreground, layout.background)).toBeGreaterThanOrEqual(4.5);
  await Promise.all([
    page.waitForURL(/\/Workbench\/Projects/),
    page.keyboard.press("Enter")
  ]);
});

test("admin project list keeps filters inside the desktop viewport and pagination usable", async ({ page }) => {
  await page.goto("/Admin/Projects?PageSize=20");

  await expect(page.locator(".page-title")).toBeVisible();
  const filterDrawer = page.locator("[data-filter-drawer-panel]");
  await expect(page.locator("[data-filter-drawer-toggle]")).toBeVisible();
  const filterFitsViewport = await filterDrawer.evaluate((element) => {
    const rect = element.getBoundingClientRect();
    return rect.left >= 0 && rect.right <= window.innerWidth;
  });
  expect(filterFitsViewport).toBeTruthy();
  await expectPaginationUsable(page);

  await Promise.all([
    page.waitForURL(/PageSize=50/, { timeout: 20000 }),
    page.locator(".pagination-size-form select").selectOption("50")
  ]);
  await expectPaginationUsable(page);

  await capture(page, "admin-projects-list");
});

test("open project report supports analysis drill-down and pagination", async ({ page }) => {
  await page.goto("/Reports/OpenProjects?PageSize=20");

  await expect(page.locator(".analysis-card-link").first()).toBeVisible();
  await Promise.all([
    page.waitForURL(/AnalysisType=/, { timeout: 15000 }),
    page.locator(".analysis-card-link").first().click()
  ]);
  await expectPaginationUsable(page);

  await capture(page, "open-project-report");
});

test("open project report defaults to priority columns and exposes the full preset", async ({ page }) => {
  await page.goto("/Reports/OpenProjects?PageSize=20");

  const table = page.locator(".open-project-report-table");
  await expect(table.locator('th[data-column="projectNumber"]')).toBeVisible();
  await expect(table.locator('th[data-column="name"]')).toBeVisible();
  await expect(table.locator('th[data-column="purchaseAmount"]')).toBeHidden();
  await expect(table.locator('th[data-column="progressDescription"]')).toBeHidden();

  await Promise.all([
    page.waitForURL(/ViewPreset=full/),
    page.getByRole("link", { name: "完整資料" }).click()
  ]);
  await expect(page.locator('th[data-column="purchaseAmount"]')).toBeVisible();
  await expect(page.locator('th[data-column="progressDescription"]')).toBeVisible();

  const geometry = await page.evaluate(() => ({
    noPageOverflow: document.documentElement.scrollWidth <= window.innerWidth,
    reportScrollsInsideWrapper: document.querySelector(".data-table-wrap")?.scrollWidth >= document.querySelector(".data-table-wrap")?.clientWidth,
    projectNumberNoCharacterWrap: getComputedStyle(document.querySelector('[data-column="projectNumber"]')).whiteSpace === "nowrap"
  }));
  expect(geometry.noPageOverflow).toBeTruthy();
  expect(geometry.reportScrollsInsideWrapper).toBeTruthy();
  expect(geometry.projectNumberNoCharacterWrap).toBeTruthy();
});

test("saved view toolbar supports keyboard focus, 200 percent zoom layout, and contrast", async ({ page }) => {
  await page.setViewportSize({ width: 720, height: 900 });
  await page.goto("/Reports/OpenProjects?PageSize=20");

  const toolbar = page.locator("[data-saved-view-bar]");
  await expect(toolbar).toBeVisible();
  const firstPreset = toolbar.getByRole("link").first();
  await firstPreset.focus();
  const focusedInsideToolbar = await page.evaluate(() => document.activeElement?.closest("[data-saved-view-bar]") !== null);
  expect(focusedInsideToolbar).toBeTruthy();
  await Promise.all([
    page.waitForURL(/ViewPreset=/),
    page.keyboard.press("Enter")
  ]);

  const layout = await page.evaluate(() => {
    const toolbarElement = document.querySelector("[data-saved-view-bar]");
    const wrapper = document.querySelector(".data-table-wrap");
    const primaryButton = toolbarElement?.querySelector(".btn-primary");
    const style = primaryButton ? getComputedStyle(primaryButton) : null;
    const parseRgb = (value) => (value.match(/\d+(?:\.\d+)?/g) || []).slice(0, 3).map(Number);
    return {
      noPageOverflow: document.documentElement.scrollWidth <= window.innerWidth,
      toolbarWraps: Boolean(toolbarElement && toolbarElement.scrollWidth <= toolbarElement.clientWidth + 1),
      tableCanScroll: Boolean(wrapper && wrapper.scrollWidth >= wrapper.clientWidth),
      foreground: style ? parseRgb(style.color) : [],
      background: style ? parseRgb(style.backgroundColor) : []
    };
  });

  expect(layout.noPageOverflow).toBeTruthy();
  expect(layout.toolbarWraps).toBeTruthy();
  expect(layout.tableCanScroll).toBeTruthy();
  expect(contrastRatio(layout.foreground, layout.background)).toBeGreaterThanOrEqual(4.5);
});

test("project detail audit trail can be filtered", async ({ page }) => {
  await page.goto("/Admin/Projects?PageSize=10");
  await page.locator('.data-table tbody tr a[href*="/Details"]').first().click();

  await expect(page.locator(".gantt-panel")).toHaveCount(0);
  await expect(page.locator(".audit-filter-panel")).toHaveCount(0);
  await Promise.all([
    page.waitForURL(/Tab=audit/),
    page.locator('[data-detail-tab-target="audit"]').click()
  ]);
  await expect(page.locator(".audit-filter-panel")).toBeVisible();
  await Promise.all([
    page.waitForURL(/AuditPageSize=10/),
    page.locator('select[name="AuditPageSize"]').selectOption("10")
  ]);
  await page.locator('input[name="AuditKeyword"]').fill("admin");
  await Promise.all([
    page.waitForURL(/AuditKeyword=admin/),
    page.locator('.audit-filter-actions button[type="submit"]').click()
  ]);
  await page.waitForLoadState("networkidle");
  await expect(page.locator(".audit-filter-panel")).toBeVisible();

  await capture(page, "project-detail-audit");
});

test("project detail gantt loads on demand without desktop overflow", async ({ page }) => {
  await page.goto("/Admin/Projects?PageSize=10");
  await page.locator('.data-table tbody tr a[href*="/Details"]').first().click();

  await expect(page.locator(".gantt-panel")).toHaveCount(0);
  await Promise.all([
    page.waitForURL(/Tab=gantt/),
    page.locator('[data-detail-tab-target="gantt"]').click()
  ]);
  await expect(page.locator(".gantt-panel")).toBeVisible();
  await expect(page.locator(".gantt-progress-line")).toHaveCount(1);
  await expect(page.locator(".gantt-legend-milestone")).toBeVisible();
  const ganttTable = page.locator(".gantt-edit-table");
  await expect(ganttTable.locator("thead th", { hasText: "里程碑" })).toBeVisible();
  await expect(ganttTable.locator("thead th", { hasText: "負責人" })).toBeVisible();
  await expect(ganttTable.locator("thead th", { hasText: "前置工作" })).toBeVisible();
  await expect(ganttTable.locator("thead th", { hasText: "實際開始" })).toBeVisible();
  await expect(page.locator('input[name="GanttInput.RowVersion"]')).toHaveCount(1);

  const ganttGeometry = await page.evaluate(() => {
    const overlay = document.querySelector(".gantt-chart-overlay");
    const svg = document.querySelector(".gantt-progress-line");
    const overlayRect = overlay?.getBoundingClientRect();
    const svgRect = svg?.getBoundingClientRect();
    return {
      noPageOverflow: document.documentElement.scrollWidth <= window.innerWidth,
      widthsMatch: Boolean(overlayRect && svgRect && Math.abs(overlayRect.width - svgRect.width) < 1),
      markerCount: document.querySelectorAll(".gantt-progress-marker").length,
      progressLabels: Array.from(document.querySelectorAll(".gantt-bar-progress-label")).map((label) => {
        const row = label.closest(".gantt-chart-row");
        const bar = row?.querySelector(".gantt-bar");
        const fill = bar?.querySelector("i");
        const labelRect = label.getBoundingClientRect();
        const barRect = bar?.getBoundingClientRect();
        const fillRect = fill?.getBoundingClientRect();

        return {
          transparent: getComputedStyle(label).backgroundColor === "rgba(0, 0, 0, 0)",
          clearsBar: Boolean(barRect && labelRect.bottom <= barRect.top),
          followsFillEnd: Boolean(fillRect && Math.abs(labelRect.left + labelRect.width / 2 - fillRect.right) < 2.5)
        };
      })
    };
  });

  expect(ganttGeometry.noPageOverflow).toBeTruthy();
  expect(ganttGeometry.widthsMatch).toBeTruthy();
  expect(ganttGeometry.markerCount).toBeGreaterThan(0);
  expect(ganttGeometry.progressLabels.length).toBeGreaterThan(0);
  expect(ganttGeometry.progressLabels.every((label) => label.transparent)).toBeTruthy();
  expect(ganttGeometry.progressLabels.every((label) => label.clearsBar)).toBeTruthy();
  expect(ganttGeometry.progressLabels.every((label) => label.followsFillEnd)).toBeTruthy();
  await captureElement(page, ".gantt-chart-wrap", "project-detail-gantt-chart");
  await capture(page, "project-detail-gantt");
});

test("project collaboration timeline supports keyboard entry and concurrency fields", async ({ page }) => {
  await page.goto("/Admin/Projects?PageSize=10");
  await page.locator('.data-table tbody tr a[href*="/Details"]').first().click();

  await Promise.all([
    page.waitForURL(/Tab=collaboration/),
    page.locator('[data-detail-tab-target="collaboration"]').click()
  ]);
  await expect(page.locator(".collaboration-panel")).toBeVisible();
  const content = page.locator("#collaboration-content");
  await content.focus();
  await expect(content).toBeFocused();
  await content.fill("Playwright 協作記錄測試");
  await expect(page.getByRole("button", { name: "新增記錄" })).toBeVisible();

  const layout = await page.evaluate(() => ({
    noPageOverflow: document.documentElement.scrollWidth <= window.innerWidth,
    liveTimelinePresent: document.querySelector(".collaboration-timeline") !== null || document.querySelector(".collaboration-empty") !== null
  }));
  expect(layout.noPageOverflow).toBeTruthy();
  expect(layout.liveTimelinePresent).toBeTruthy();
});

test("maintenance orders follow the grouped template and reflow hidden columns", async ({ page }) => {
  await page.goto("/Admin/MaintenanceOrders?PageSize=20");

  const table = page.locator("#maintenance-orders-table");
  await expect(table).toBeVisible();
  await expect(table.locator('th[data-column-group="remoteScope softwareScope hardwareScope"]')).toHaveText("維保範圍");
  await expect(table.locator('tbody td[data-column="contractNumber"]').first()).not.toHaveText("");
  await expect(table.locator('tbody td[data-column="description"]').first()).not.toHaveText("");

  const manager = page.locator("[data-column-manager]");
  await manager.locator("[data-column-manager-toggle]").click();
  await manager.locator('[data-column-key="updatedAt"]').uncheck();
  await manager.locator('[data-column-key="updatedBy"]').uncheck();

  await expect(table.locator('th[data-column-group="updatedAt updatedBy"]')).toBeHidden();
  await expect(table.locator('tbody td[data-column="updatedAt"]').first()).toBeHidden();
  await expect(table.locator('tbody td[data-column="updatedBy"]').first()).toBeHidden();

  const layout = await table.evaluate((element) => {
    const wrapper = element.closest(".data-table-wrap");
    if (wrapper) {
      wrapper.scrollLeft = wrapper.scrollWidth;
    }
    const visibleColumns = Array.from(element.querySelectorAll("tbody tr:first-child td"))
      .filter((cell) => getComputedStyle(cell).display !== "none").length;
    const visibleCells = Array.from(element.querySelectorAll("tbody tr:first-child td"))
      .filter((cell) => getComputedStyle(cell).display !== "none");
    const actionCell = visibleCells.at(-1);
    const previousCell = visibleCells.at(-2);
    const actionRect = actionCell?.getBoundingClientRect();
    const previousRect = previousCell?.getBoundingClientRect();
    return {
      visibleColumns,
      stateCount: Number(element.getAttribute("data-visible-column-count")),
      containedByWrapper: Boolean(wrapper && element.getBoundingClientRect().width <= wrapper.scrollWidth + 1),
      noPageOverflow: document.documentElement.scrollWidth <= window.innerWidth,
      actionIsStatic: actionCell ? getComputedStyle(actionCell).position === "static" : false,
      actionDoesNotOverlap: Boolean(actionRect && previousRect && previousRect.right <= actionRect.left + 1)
    };
  });

  expect(layout.visibleColumns).toBeGreaterThan(10);
  expect(layout.stateCount).toBeGreaterThan(10);
  expect(layout.containedByWrapper).toBeTruthy();
  expect(layout.noPageOverflow).toBeTruthy();
  expect(layout.actionIsStatic).toBeTruthy();
  expect(layout.actionDoesNotOverlap).toBeTruthy();
  await capture(page, "maintenance-orders-template");
});
