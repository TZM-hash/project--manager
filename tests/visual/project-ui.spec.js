const { test, expect } = require("@playwright/test");
const fs = require("fs");
const path = require("path");

const baseURL = process.env.BASE_URL || "http://localhost:62382";
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

test.beforeEach(async ({ page }) => {
  await signIn(page);
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
  expect(layout.stateCount).toBe(16);
  expect(layout.containedByWrapper).toBeTruthy();
  expect(layout.noPageOverflow).toBeTruthy();
  expect(layout.actionIsStatic).toBeTruthy();
  expect(layout.actionDoesNotOverlap).toBeTruthy();
  await capture(page, "maintenance-orders-template");
});
