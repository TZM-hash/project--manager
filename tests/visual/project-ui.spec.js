const { test, expect } = require("@playwright/test");
const path = require("path");

const baseURL = process.env.BASE_URL || "http://localhost:62382";
const userName = process.env.VISUAL_USER || "admin";
const password = process.env.VISUAL_PASSWORD || "ChangeMe123!";
const outputDir = process.env.VISUAL_OUTPUT_DIR || path.join(process.cwd(), "..", "..", "artifacts", "visual");

test.use({
  baseURL,
  viewport: { width: 1440, height: 900 }
});

async function signIn(page) {
  await page.goto("/Identity/Account/Login");
  await page.getByLabel("账号").fill(userName);
  await page.getByLabel("密码").fill(password);
  await page.getByRole("button", { name: "登录" }).click();
  await expect(page.getByText("退出")).toBeVisible();
}

async function capture(page, name) {
  await page.screenshot({
    path: path.join(outputDir, `${name}.png`),
    fullPage: true
  });
}

test.beforeEach(async ({ page }) => {
  await signIn(page);
});

test("项目管理列表布局紧凑且分页可操作", async ({ page }) => {
  await page.goto("/Admin/Projects?PageSize=20");

  await expect(page.getByRole("heading", { name: "项目管理" })).toBeVisible();
  await expect(page.locator(".pagination-wrap")).toBeVisible();
  await page.locator(".pagination-size-form select").selectOption("50");
  await expect(page).toHaveURL(/PageSize=50/);

  await capture(page, "admin-projects-list");
});

test("未结案报表分析卡和分页可见", async ({ page }) => {
  await page.goto("/Reports/OpenProjects?PageSize=20");

  await expect(page.getByText("报表分析视图")).toBeVisible();
  await expect(page.locator(".analysis-card-link").first()).toBeVisible();
  await page.locator(".analysis-card-link").first().click();
  await expect(page).toHaveURL(/AnalysisType=/);
  await expect(page.locator(".pagination-wrap")).toBeVisible();

  await capture(page, "open-project-report");
});

test("项目详情操作记录时间线可筛选", async ({ page }) => {
  await page.goto("/Admin/Projects?PageSize=10");
  await page.getByRole("link", { name: "详情" }).first().click();

  await page.getByRole("tab", { name: "操作记录" }).click();
  await expect(page.locator(".audit-filter-panel")).toBeVisible();
  await page.locator("input[name='AuditKeyword']").fill("项目");
  await page.getByRole("button", { name: "筛选记录" }).click();
  await expect(page.locator(".audit-filter-panel")).toBeVisible();

  await capture(page, "project-detail-audit");
});
