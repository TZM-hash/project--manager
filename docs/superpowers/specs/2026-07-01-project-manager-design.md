# Project Manager ASP.NET System Design

## 1. Goal

Build a compact, efficient C# ASP.NET Core system for internal company project management. The system supports multi-user login, role-based access, project tracking, purchase request tracking, monthly manual settlement snapshots, Excel export, printable reports, project status visualization, and administrator-controlled display styling.

The first version targets company intranet deployment with SQL Server.

## 2. Recommended Architecture

Use a single ASP.NET Core web application with two logical entrances:

- Admin entrance: for administrators to maintain users, roles, project master data, status definitions, and display style settings.
- Web workbench entrance: for project staff to update assigned project information and for leaders/query users to view projects, status charts, monthly settlement reports, and open-project statistics.

Technology stack:

- ASP.NET Core Razor Pages for server-rendered pages.
- ASP.NET Core Identity for login, password hashing, password change, and role management.
- Entity Framework Core for SQL Server data access.
- SQL Server for the database.
- ClosedXML for Excel export.
- Browser print styles for printable reports.
- Lightweight JavaScript and CSS for dynamic status charts and configurable status colors.

This stack is preferred because it keeps deployment simple for an intranet system, avoids a separate frontend build pipeline, and still supports future expansion.

## 3. Roles and Permissions

Initial roles:

- Administrator: manages all users, roles, projects, status definitions, monthly settlements, reports, and style settings.
- ProjectStaff: views projects assigned to them and updates project progress, progress description, and non-financial notes. ProjectStaff does not edit financial amounts in version 1.
- Leader: views project lists, project detail, status charts, monthly settlement reports, and open-project statistics.
- Viewer: read-only access to authorized project and report pages.

Password behavior:

- Users log in with username and password.
- All users can change their own password.
- Administrators can create users, reset passwords, enable or disable accounts, and assign roles.

## 4. Main Functional Areas

### 4.1 Project Master Data

Each project stores:

- Year
- Parent case number
- Project number
- Project name
- Project personnel
- Project progress percentage
- Project amount
- Collection percentage
- Project progress description
- Updated by user
- Closed year/month
- Last updated time
- Project settlement/status state

Project status must be extensible. Initial statuses include:

- Created / 已立案
- PurchaseRequested / 已请购
- Coding / 代码中
- TrialRun / 试车中
- WaitingCollection / 待收款
- Closed / 已结案

The Closed status is a special terminal marker and should display in red by default. The database must allow more statuses to be added later without code changes.

### 4.2 Purchase Requests

Each project can have multiple purchase requests.

Each purchase request stores:

- Purchase request number
- Purchase type: InternalPurchase or ExternalPurchase
- Purchase staff
- Purchase amount
- Sub-case contact person
- Payment percentage
- Actual paid amount
- Notes

Purchase requests are child records of the project so a project can have zero, one, or many purchase entries.

### 4.3 Monthly Manual Settlement

The system supports manual monthly settlement of project progress and financial data.

Requirements:

- A user selects year and month, then triggers settlement.
- Settlement can run multiple times for the same month.
- Each run creates a new settlement batch rather than overwriting prior batches.
- Each batch captures snapshot rows for projects at the time of settlement.
- Snapshot data includes parent case number, project progress, project amount, collection percentage, status, closed year/month, purchase request totals, sub-case contact summary, payment percentage, actual paid amount, progress description, and updater.
- Batches record created by user and created time.

This provides an auditable month-end history while still allowing repeated corrections.

### 4.4 Reports

Required reports:

- Monthly settlement report by year/month and settlement batch.
- Open project report for projects not marked Closed.
- Open project monthly statistics by status, amount, collection percentage, purchase amount, and paid amount.
- Project detail report with purchase request breakdown.

Exports and printing:

- Excel export for settlement and open-project reports.
- Printable HTML pages with print-specific CSS.
- Report filters for year, month, parent case number, project number, project name, personnel, status, and closed/open state.
- Default settlement Excel columns: year, month, batch number, parent case number, project number, project name, personnel, progress percentage, project amount, collection percentage, status, closed year/month, purchase request summary, purchase total, sub-case contact summary, payment percentage summary, actual paid total, progress description, updater, source update time.
- Default open-project Excel columns: year, parent case number, project number, project name, personnel, progress percentage, project amount, collection percentage, status, closed year/month, purchase total, sub-case contact summary, actual paid total, progress description, updater, last updated time.

### 4.5 Project Dynamic Status Chart

Each project detail page shows a visual status chart.

Design:

- Render the configured status list in order.
- Highlight completed/current states based on the project's current status.
- Show Closed in red by default.
- Support future statuses from the status definition table.
- Keep the first version server-rendered with small JavaScript enhancements only if needed.

### 4.6 Display Style Settings

Administrators can configure frontend status display settings:

- Status text color.
- Status background color.
- Bold flag.
- Display order.
- Active/inactive flag.

These settings are used consistently in project lists, project detail, status charts, and reports.

## 5. Data Model

Core tables:

- AspNetUsers and ASP.NET Identity tables.
- Projects.
- ProjectAssignments.
- ProjectStatuses.
- ProjectStatusStyles.
- PurchaseRequests.
- MonthlySettlementBatches.
- MonthlySettlementItems.
- AuditLogs.

Recommended important fields:

Projects:

- Id
- Year
- ParentCaseNumber
- ProjectNumber
- Name
- ProgressPercent
- ProjectAmount
- CollectionPercent
- ProgressDescription
- StatusId
- UpdatedByUserId
- ClosedYearMonth
- UpdatedAt
- CreatedAt
- IsDeleted

ProjectAssignments:

- Id
- ProjectId
- UserId
- RoleInProject

ProjectStatuses:

- Id
- Code
- Name
- SortOrder
- IsClosed
- IsActive

ProjectStatusStyles:

- Id
- StatusId
- TextColor
- BackgroundColor
- IsBold

PurchaseRequests:

- Id
- ProjectId
- RequestNumber
- PurchaseType
- PurchaseStaffUserId
- PurchaseAmount
- SubCaseContactUserId
- PaymentPercent
- ActualPaidAmount
- Notes
- CreatedAt
- UpdatedAt

MonthlySettlementBatches:

- Id
- Year
- Month
- BatchNumber
- CreatedByUserId
- CreatedAt
- Notes

MonthlySettlementItems:

- Id
- BatchId
- ProjectId
- ParentCaseNumber
- ProjectNumber
- ProjectName
- ProjectPersonnelText
- ProgressPercent
- ProjectAmount
- CollectionPercent
- StatusName
- IsClosed
- ClosedYearMonth
- PurchaseRequestSummary
- PurchaseAmountTotal
- SubCaseContactSummary
- PaymentPercentSummary
- ActualPaidAmountTotal
- ProgressDescription
- UpdatedByUserName
- SourceUpdatedAt

AuditLogs:

- Id
- UserId
- Action
- EntityName
- EntityId
- Description
- CreatedAt

## 6. Key Workflows

Login and password:

1. User signs in.
2. System applies role-based menu and page permissions.
3. User can change their own password.
4. Administrator can reset user passwords and disable accounts.

Project maintenance:

1. Administrator creates or edits a project.
2. Administrator assigns project staff.
3. Project staff updates progress and progress description for assigned projects.
4. System records updater and update time.

Monthly settlement:

1. Administrator selects year/month.
2. System previews projects included in the settlement.
3. Administrator confirms settlement.
4. System creates a new batch and snapshot items.
5. User exports Excel or opens print page from the batch.

Open project statistics:

1. User selects year/month or current date.
2. System filters projects whose status is not Closed.
3. System groups and totals by status and financial fields.
4. User exports or prints the report.

## 7. Error Handling and Validation

Validation rules:

- Project number is required and unique within a year.
- Parent case number is optional in version 1, but must be included in filters, detail pages, exports, and settlement snapshots when provided.
- Project amount, purchase amount, actual paid amount, progress percentage, collection percentage, and payment percentage cannot be negative.
- Percentage fields should be between 0 and 100.
- Purchase request number is required for each purchase request.
- Closed year/month must use month precision. When the current status has ProjectStatuses.IsClosed=true, closed year/month is required.
- Settlement month must be between 1 and 12.
- Closed status is controlled by the ProjectStatuses.IsClosed flag, not by hard-coded text.

Error handling:

- Show user-friendly validation messages on forms.
- Log unexpected errors.
- Keep settlement creation transactional so a batch is either fully created or not created at all.

## 8. Testing Strategy

Initial tests should cover:

- Project creation and validation.
- Parent case number and closed year/month persistence.
- Multiple purchase requests per project.
- Sub-case contact person persistence on purchase requests.
- Status style rendering rules.
- Closed project filtering.
- Monthly settlement batch creation with repeat runs.
- Excel export produces expected report headers and rows.
- Role-based access for admin, project staff, leader, and viewer.

Manual verification should include:

- Login and password change.
- Administrator user creation and role assignment.
- Project list filtering.
- Project detail status chart.
- Monthly settlement export and browser print view.

## 9. First Implementation Scope

Version 1 should include:

- ASP.NET Core project scaffold.
- SQL Server EF Core configuration.
- Identity login and password management.
- Seeded roles and initial status definitions.
- Admin project CRUD.
- Project purchase request child records.
- Web workbench project list/detail.
- Project status chart.
- Monthly settlement batch creation and settlement list/detail.
- Excel export for monthly settlement and open-project reports.
- Printable report pages.
- Admin status style settings.

Out of scope for the first version:

- External customer portal.
- Mobile app.
- Complex approval workflow.
- Real-time notifications.
- Document attachment management.
- Separate frontend SPA.

## 10. Implementation Defaults

The first implementation uses the following defaults:

- Use a restrained intranet business style with neutral backgrounds, compact tables, and configurable status colors.
- Use the default Excel column orders listed in the report section.
- ProjectStaff can update project progress and progress description only; administrators maintain financial fields and purchase amounts.
- Use soft delete for projects and purchase requests.
- Seed Closed / 已结案 with red text and bold enabled by default.
