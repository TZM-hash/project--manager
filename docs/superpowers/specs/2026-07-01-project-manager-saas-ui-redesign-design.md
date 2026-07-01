# Project Manager SaaS UI Redesign Design

## Goal

Redesign the existing ASP.NET Core Razor Pages project management system into a bright, professional SaaS-style interface. The redesign should make the product feel modern, polished, and pleasant to use while preserving the current business workflows, permissions, data model, routes, tests, and server-side rendering approach.

The target feel is a mature internal SaaS product: clear, calm, data-oriented, and visually refined. It should not look like a marketing website, a decorative landing page, or a dark futuristic dashboard.

## Visual Direction

Use a bright professional SaaS style:

- Light neutral application background.
- White primary content surfaces.
- Deep ink text for hierarchy and readability.
- Blue primary actions.
- Green, amber, red, and muted gray status accents.
- Subtle borders and restrained shadows.
- Consistent 8px-or-smaller radii for cards, tables, filters, and inputs.
- Dense but breathable spacing for long business tables.

Avoid oversized hero sections, decorative gradients, one-note purple palettes, nested cards, and ornamental visual elements that do not help the workflow.

## Scope

This redesign covers the application shell and the most visible Razor Pages UI:

- Shared layout and navigation.
- Home dashboard.
- Admin project list.
- Admin project create/edit form.
- Admin project details.
- Workbench project list and details.
- Status badge and status timeline partials.
- Settlement and report list/detail/print tables through shared table and page styles.
- Account/login shell where practical.

The redesign does not change:

- Entity models.
- EF Core migrations.
- Identity and authorization rules.
- Project, settlement, report, or Excel export services.
- Existing route structure.
- Database connection settings.
- Business validation rules.

## Information Architecture

Keep the existing role-based navigation but make it feel more product-like:

- Brand area: "项目管理系统" as the clear product identity.
- Primary nav: 首页, 用户管理, 项目管理, 状态设置, 工作台, 月结, 报表.
- User/account actions remain on the right.
- The active and hover states should make the current section easy to understand.

Pages use a consistent header pattern:

- Left side: title and short contextual subtitle.
- Right side: primary action button or compact action group.
- Below the header: optional metric cards, filters, tables, or details.

## Page Designs

### Home Dashboard

The home page becomes a true SaaS dashboard:

- A compact welcome/header band with product purpose and deployment context.
- Four metric cards for project count, open projects, active statuses, and settlement batches.
- Role-aware entrance panels for admin, workbench, and settlement/report workflows.
- Panels should feel like actionable product modules, not generic Bootstrap cards.

### Project List Pages

Project lists should behave like modern data workspaces:

- The filter form becomes a clean toolbar inside a white surface.
- Inputs align consistently and remain compact.
- The table uses a light sticky-feeling header style, row hover, clear numeric alignment, and quieter row borders.
- Status values render as status badges instead of plain text where data is available.
- Actions use compact outline buttons with a consistent visual rhythm.

### Project Form

The project form is split into readable sections:

- Basic project information.
- Status, progress, and financial fields.
- Progress description.
- Purchase request table.

The purchase request table remains editable and dense, but inputs should be visually calmer and better aligned. The page should make a large business form feel organized rather than crowded.

### Project Details And Workbench Details

Detail pages should emphasize project state and quick comprehension:

- Header with project number, name, and actions.
- Status timeline in a polished horizontal component.
- Summary information in a structured detail grid.
- Financial/progress values highlighted as small metric tiles where practical.
- Purchase request summary table styled consistently with the rest of the app.

### Reports And Settlements

Reports and settlement pages should inherit the new shell, filter, and table styles:

- Keep report columns and export/print behavior unchanged.
- Improve visual hierarchy around filters, export buttons, and tables.
- Print CSS remains plain and high-contrast for paper output.

## Design System

### Tokens

Use CSS custom properties in `wwwroot/css/site.css` for:

- Background: app background, surface, elevated surface.
- Text: primary, secondary, muted.
- Border: default and strong.
- Accent: primary, primary hover, primary soft.
- Status: success, warning, danger, info, neutral.
- Radius: small and medium.
- Shadow: subtle card/table elevation.

### Components

Create reusable visual patterns through CSS classes, not new backend abstractions:

- `.app-shell`, `.app-container`, `.app-navbar`.
- `.page-header`, `.page-title`, `.page-subtitle`, `.page-actions`.
- `.metric-card`, `.metric-label`, `.metric-value`.
- `.module-grid`, `.module-card`, `.module-actions`.
- `.filter-panel`.
- `.data-table`.
- `.detail-panel`, `.detail-grid`.
- `.form-section`.
- Improved `.status-badge` and `.status-timeline`.

Existing Bootstrap utilities can remain, but custom classes should provide the product identity.

## Interaction And Accessibility

- Buttons, links, form controls, and nav items must have clear focus states.
- Hover states should be visible but restrained.
- Tables must remain readable on narrow screens by using horizontal overflow where needed.
- Text must not overflow buttons, cards, or table controls.
- Print output must stay functional and plain.

## Implementation Plan Boundary

The implementation should be CSS-first, with targeted Razor markup updates only where needed for structure and semantics. The safest order is:

1. Update shared layout and global design tokens.
2. Restyle home dashboard.
3. Restyle project list, project details, and workbench details.
4. Restyle project form sections and purchase table.
5. Apply shared table/filter/page styles to settlement and report pages.
6. Build and run available tests.
7. Run the app and visually inspect the primary screens.

## Testing

Run the existing .NET test suite after UI changes:

```powershell
.\scripts\dotnet.ps1 test ProjectManager.sln
```

Also run a build:

```powershell
.\scripts\dotnet.ps1 build ProjectManager.sln
```

If the app can be launched locally, inspect:

- Home dashboard.
- Admin project list.
- Admin project create/edit form.
- Workbench project details.
- Open project report.
- Settlement list or detail page.

## Open Decisions

The user has approved the bright professional SaaS direction. No further product-scope decisions are needed before implementation.
