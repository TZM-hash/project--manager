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

test("admin project list keeps filters compact and pagination usable", async ({ page }) => {
  await page.goto("/Admin/Projects?PageSize=20");

  await expect(page.locator(".page-title")).toBeVisible();
  await expect(page.locator("[data-filter-drawer-panel]")).not.toBeVisible();
  await expectPaginationUsable(page);

  await page.locator(".pagination-size-form select").selectOption("50");
  await expect(page).toHaveURL(/PageSize=50/);
  await expectPaginationUsable(page);

  await capture(page, "admin-projects-list");
});

test("open project report supports analysis drill-down and pagination", async ({ page }) => {
  await page.goto("/Reports/OpenProjects?PageSize=20");

  await expect(page.locator(".analysis-card-link").first()).toBeVisible();
  await page.locator(".analysis-card-link").first().click();
  await expect(page).toHaveURL(/AnalysisType=/);
  await expectPaginationUsable(page);

  await capture(page, "open-project-report");
});

test("project detail audit trail can be filtered", async ({ page }) => {
  await page.goto("/Admin/Projects?PageSize=10");
  await page.locator('.data-table tbody tr a[href*="/Details"]').first().click();

  await page.locator('[data-detail-tab-target="audit"]').click();
  await expect(page.locator(".audit-filter-panel")).toBeVisible();
  await page.locator('input[name="AuditKeyword"]').fill("admin");
  await page.locator('.audit-filter-actions button[type="submit"]').click();
  await expect(page.locator(".audit-filter-panel")).toBeVisible();

  await capture(page, "project-detail-audit");
});
