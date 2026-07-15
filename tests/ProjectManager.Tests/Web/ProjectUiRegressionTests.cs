using FluentAssertions;

namespace ProjectManager.Tests.Web;

public sealed class ProjectUiRegressionTests
{
    [Fact]
    public void Planning_project_surfaces_use_provisional_leader_and_vendor_labels()
    {
        var planningFiles = new[]
        {
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "PlanningProjects", "Index.cshtml"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "PlanningProjects", "Index.cshtml.cs"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "PlanningProjects", "Create.cshtml"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "PlanningProjects", "Create.cshtml.cs"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "PlanningProjects", "Edit.cshtml"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "PlanningProjects", "Edit.cshtml.cs"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "PlanningProjects", "Import.cshtml"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "PlanningProjects", "Print.cshtml"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "PlanningProjects", "PrintList.cshtml")
        };
        var content = string.Join("\n", planningFiles);

        content.Should().Contain("暫定負責人");
        content.Should().Contain("暫定廠商");
        content.Should().NotContain(">專案負責人<");
        content.Should().NotContain("Name = \"專案負責人\"");
        content.Should().NotContain(">廠商<");
        content.Should().NotContain("Name = \"廠商\"");
    }

    [Fact]
    public void Planning_project_list_supports_optional_columns_and_standard_display_controls()
    {
        var page = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "PlanningProjects", "Index.cshtml");
        var css = FrontendAssetStructureTests.ReadCssLayers();

        page.Should().Contain("id=\"planning-projects-table\"");
        page.Should().Contain("data-column-manager-table");
        page.Should().Contain("data-column-manager-toggle");
        page.Should().Contain("data-row-spacing-toggle");
        page.Should().Contain("data-column=\"latestPeriod\"");
        page.Should().Contain("data-column=\"historyCount\"");
        page.Should().Contain("data-column=\"createdAt\"");
        css.Should().Contain(".planning-projects-table {\n  min-width: 76rem;");
        css.Should().Contain("[data-column=\"latestPeriod\"]");
        css.Should().Contain("min-width: 7.5rem;");
    }

    [Fact]
    public void Planning_project_and_existing_tables_place_column_management_before_row_spacing()
    {
        var pages = new[]
        {
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "PlanningProjects", "Index.cshtml"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Index.cshtml"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "Projects", "Index.cshtml"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Reports", "OpenProjects", "Index.cshtml"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "MaintenanceOrders", "Index.cshtml")
        };

        pages.Should().OnlyContain(page =>
            page.IndexOf("data-column-manager-toggle", StringComparison.Ordinal) >= 0 &&
            page.IndexOf("data-column-manager-toggle", StringComparison.Ordinal) <
            page.IndexOf("data-row-spacing-toggle", StringComparison.Ordinal));
    }

    [Fact]
    public void Planning_project_print_pages_use_the_dedicated_report_hierarchy()
    {
        var single = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "PlanningProjects", "Print.cshtml");
        var list = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "PlanningProjects", "PrintList.cshtml");
        var css = FrontendAssetStructureTests.ReadCssLayers();

        single.Should().Contain("planning-print-report");
        single.Should().Contain("planning-print-summary");
        single.Should().NotContain("planning-print-description");
        single.Should().NotContain("最新說明");
        single.Should().NotContain("上期說明");
        single.Should().Contain("本期記錄");
        single.Should().NotContain("<td class=\"text-nowrap\">@record.CreatedAt");
        single.Should().Contain("@@page {\n        size: A4 landscape;");
        list.Should().Contain("planning-print-report");
        list.Should().Contain("planning-print-list-table");
        list.IndexOf(">項次<", StringComparison.Ordinal)
            .Should().BeLessThan(list.IndexOf(">專案名稱<", StringComparison.Ordinal));
        list.IndexOf(">專案名稱<", StringComparison.Ordinal)
            .Should().BeLessThan(list.IndexOf(">暫定負責人<", StringComparison.Ordinal));
        list.IndexOf(">暫定負責人<", StringComparison.Ordinal)
            .Should().BeLessThan(list.IndexOf(">暫定廠商<", StringComparison.Ordinal));
        list.IndexOf(">暫定廠商<", StringComparison.Ordinal)
            .Should().BeLessThan(list.IndexOf(">進度說明<", StringComparison.Ordinal));
        list.Should().Contain("<th class=\"planning-print-col-vendor\">暫定廠商</th>\n                <th>進度說明</th>");
        single.Should().Contain("URLSearchParams");
        single.Should().Contain("has('preview')");
        single.Should().Contain("document.createTreeWalker");
        single.Should().Contain("converter(node.nodeValue)");
        single.Should().NotContain("converter.convertElement");
        list.Should().Contain("URLSearchParams");
        list.Should().Contain("has('preview')");
        css.Should().Contain(".planning-print-report");
        css.Should().Contain("width: min(1100px, calc(100% - 2rem));");
        css.Should().Contain(".planning-print-list-table");
        css.Should().Contain(".print-table-list.planning-print-history-table th");
        css.Should().Contain(".print-table-list.planning-print-list-table th");
        css.Should().Contain(".planning-print-summary-item:last-child");
        css.Should().NotContain("page: planning-detail;");
        css.Should().Contain(".planning-print-history-table th:nth-child(2)");
        css.Should().Contain("width: 55%;");
    }

    [Fact]
    public void Gantt_chart_uses_progress_threshold_colors_and_compact_inline_alerts()
    {
        var partial = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_ProjectGanttPanel.cshtml");
        var workbenchDetails = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "Projects", "Details.cshtml");
        var adminDetails = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Details.cshtml");
        var css = FrontendAssetStructureTests.ReadCssLayers();

        partial.Should().Contain("bar.ProgressPercent >= 80");
        partial.Should().Contain("bar.ProgressPercent >= 50");
        partial.Should().Contain("bar.ProgressPercent >= 20");
        partial.Should().Contain("gantt-progress-start");
        partial.Should().Contain("gantt-progress-low");
        partial.Should().Contain("gantt-progress-mid");
        partial.Should().Contain("gantt-progress-high");
        partial.Should().Contain("gantt-progress-complete");
        partial.Should().Contain("gantt-task-alerts");
        css.Should().Contain(".gantt-progress-start { --gantt-progress-color: #ef4444;");
        css.Should().Contain(".gantt-progress-low { --gantt-progress-color: #f59e0b;");
        css.Should().Contain(".gantt-progress-mid { --gantt-progress-color: #2563eb;");
        css.Should().Contain(".gantt-progress-high { --gantt-progress-color: #14b8a6;");
        css.Should().Contain(".gantt-progress-complete { --gantt-progress-color: #16a34a;");
        css.Should().Contain(".gantt-task-alerts {\n  display: flex;");
        css.Should().Contain("min-height: 4.2rem;");
        css.Should().NotContain("min-height: 5.4rem;");
        css.Should().Contain(".gantt-panel .gantt-chart-header {\n  position: sticky;\n  top: 0;");
        workbenchDetails.Should().Contain("detail-tab-shell-gantt");
        adminDetails.Should().Contain("detail-tab-shell-gantt");
        css.Should().Contain(".detail-tab-shell-gantt > .detail-tabs {\n  position: static;");
    }

    [Fact]
    public void Shared_percent_bars_use_the_historical_five_color_thresholds()
    {
        var partial = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_PercentBar.cshtml");
        var css = FrontendAssetStructureTests.ReadCssLayers();

        partial.Should().Contain("percent >= 80");
        partial.Should().Contain("percent >= 50");
        partial.Should().Contain("percent >= 20");
        partial.Should().Contain("\"is-start\"");
        partial.Should().Contain("\"is-low\"");
        partial.Should().Contain("\"is-mid\"");
        partial.Should().Contain("\"is-high\"");
        css.Should().Contain(".percent-progress.is-start");
        css.Should().Contain("--progress-color: #ef4444;");
        css.Should().Contain(".percent-progress.is-low");
        css.Should().Contain("--progress-color: #f59e0b;");
        css.Should().Contain(".percent-progress.is-mid");
        css.Should().Contain(".percent-progress.is-high");
        css.Should().Contain("--progress-color: #14b8a6;");
    }

    [Fact]
    public void Shared_status_badges_render_each_status_configured_colors()
    {
        var partial = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_StatusBadge.cshtml");

        partial.Should().Contain("style=\"color:@textColor;background-color:@backgroundColor;font-weight:@fontWeight\"");
        partial.Should().NotContain("status-semantic-");
        partial.Should().NotContain("--status-custom-");
    }

    [Fact]
    public void Global_overviews_use_the_permission_hierarchy_instead_of_viewer_role_shortcuts()
    {
        var home = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Index.cshtml.cs");
        var workbench = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "Projects", "Index.cshtml.cs");
        var navigation = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_SidebarNavigation.cshtml");

        home.Should().Contain("CanManageAllBusinessData()");
        home.Should().NotContain("RoleNames.Viewer");
        workbench.Should().Contain("CanManageAllBusinessData()");
        workbench.Should().NotContain("canViewAll = CanEditAll || User.IsInRole(RoleNames.Viewer)");
        navigation.Should().Contain("CanManageAllBusinessData()");
        navigation.Should().Contain("IsSystemAdministrator()");
    }

    [Fact]
    public void Operation_center_exposes_persisted_progress_polling_and_authorized_downloads()
    {
        var page = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Operations", "Index.cshtml");
        var script = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "js", "pages", "operation-center.js");
        var dataExchange = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "DataExchange", "Index.cshtml.cs");
        var projects = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Index.cshtml.cs");
        var maintenance = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "MaintenanceOrders", "Index.cshtml.cs");

        page.Should().Contain("data-operation-center");
        page.Should().Contain("role=\"progressbar\"");
        page.Should().Contain("aria-live=\"polite\"");
        script.Should().Contain("/Operations/Status");
        script.Should().Contain("aria-valuenow");
        dataExchange.Should().Contain("OperationJobType.FullExport");
        dataExchange.Should().Contain("OperationJobType.FullImport");
        projects.Should().Contain("OperationJobType.ProjectBulkDelete");
        maintenance.Should().Contain("OperationJobType.MaintenanceBulkDelete");
    }

    [Fact]
    public void Quality_gate_runs_release_tests_javascript_and_accessibility_discovery()
    {
        var script = ReadRepositoryFile("scripts", "quality-gate.ps1");

        script.Should().Contain("$ErrorActionPreference = 'Stop'");
        script.Should().Contain("UTF8Encoding");
        script.Should().Contain("dotnet build");
        script.Should().Contain("dotnet test");
        script.Should().Contain("node --check");
        script.Should().Contain("npx playwright test --list");
        script.Should().Contain("RunPlaywright");
    }

    [Fact]
    public void Project_detail_exposes_collaboration_timeline_and_concurrency_reload_actions()
    {
        var collaboration = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_ProjectCollaborationPanel.cshtml");
        var gantt = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_ProjectGanttPanel.cshtml");
        var projectForm = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "_ProjectForm.cshtml");
        var progressForm = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "Projects", "EditProgress.cshtml");

        collaboration.Should().Contain("collaboration-timeline");
        collaboration.Should().Contain("AddCollaboration");
        collaboration.Should().Contain("UpdateCollaboration");
        collaboration.Should().Contain("DeleteCollaboration");
        collaboration.Should().Contain("重新載入最新版本");
        collaboration.Should().Contain("標記為重要記錄");
        collaboration.Should().Contain("CollaborationInput.Attachment");
        collaboration.Should().Contain("DownloadCollaborationAttachment");
        collaboration.Should().Contain("操作稽核");
        collaboration.Should().NotContain("@提醒");
        gantt.Should().Contain("GanttInput.RowVersion");
        gantt.Should().Contain("重新載入最新版本");
        projectForm.Should().Contain("Input.RowVersion");
        progressForm.Should().Contain("Input.RowVersion");
    }

    [Fact]
    public void Gantt_rows_keep_label_metadata_and_status_badges_in_a_compact_layout()
    {
        var themes = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "css", "themes.css");

        themes.Should().Contain(".gantt-panel .gantt-chart-row {\n  height: auto;");
        themes.Should().Contain("min-height: 4.2rem;");
        themes.Should().Contain("overflow: visible;\n  white-space: normal;");
    }

    [Fact]
    public void Home_page_prioritizes_one_conclusion_one_action_and_four_work_queues()
    {
        var page = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Index.cshtml");

        page.Should().Contain("workbench-hero");
        page.Should().Contain("Model.Workbench.HeroTitle");
        page.Should().Contain("Model.Workbench.PrimaryActionText");
        page.Should().Contain("我的逾期");
        page.Should().Contain("待處理");
        page.Should().Contain("近期節點");
        page.Should().Contain("長期未更新");
        page.Should().NotContain("metric-grid dashboard-grid");
    }

    [Fact]
    public void Saved_view_bar_exposes_presets_personal_actions_and_accessible_hooks()
    {
        var partial = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_SavedDataViewBar.cshtml");
        var script = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "js", "components", "saved-views.js");
        var site = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "js", "site.js");

        partial.Should().Contain("data-saved-view-bar");
        partial.Should().Contain("系統預設檢視");
        partial.Should().Contain("個人檢視");
        partial.Should().Contain("asp-page-handler=\"SaveView\"");
        partial.Should().Contain("asp-page-handler=\"DeleteView\"");
        partial.Should().Contain("asp-page-handler=\"SetDefaultView\"");
        partial.Should().Contain("aria-label=\"選擇個人檢視\"");
        script.Should().Contain("data-current-filters");
        script.Should().Contain("data-processing-label");
        site.Should().Contain("[data-saved-view-bar]");
        site.Should().Contain("saved-views.js");
    }

    [Fact]
    public void Supported_data_pages_render_server_backed_saved_views()
    {
        var reportPage = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Reports", "OpenProjects", "Index.cshtml");
        var reportTable = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Reports", "OpenProjects", "_OpenProjectsTable.cshtml");
        var pages = new[]
        {
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Index.cshtml"),
            reportPage + reportTable,
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "Projects", "Index.cshtml"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "MaintenanceOrders", "Index.cshtml")
        };

        pages.Should().OnlyContain(page => page.Contains("partial name=\"_SavedDataViewBar\"", StringComparison.Ordinal));
        pages.Should().OnlyContain(page => page.Contains("data-initial-visible-columns", StringComparison.Ordinal));
        pages.Should().OnlyContain(page => page.Contains("data-initial-row-density", StringComparison.Ordinal));
    }

    [Fact]
    public void Print_layout_loads_the_split_stylesheets_used_by_all_print_pages()
    {
        var layout = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_PrintLayout.cshtml");
        var printPages = new[]
        {
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Reports", "OpenProjects", "Print.cshtml"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Settlements", "Print.cshtml"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Report.cshtml"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "PlanningProjects", "Print.cshtml"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "PlanningProjects", "PrintList.cshtml"),
            ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "Projects", "GanttPrint.cshtml")
        };

        layout.Should().Contain("~/css/base.css");
        layout.Should().Contain("~/css/components.css");
        layout.Should().Contain("~/css/pages.css");
        layout.Should().Contain("~/css/themes.css");
        layout.Should().Contain("~/ProjectManager.Web.styles.css");
        layout.Should().NotContain("~/css/site.css");
        printPages.Should().OnlyContain(page => page.Contains("Layout = \"_PrintLayout\"", StringComparison.Ordinal));
        printPages[0].Should().Contain("ViewData[\"VisibleColumns\"]");
        printPages[0].Should().Contain("\"collectionPercent\"");
        printPages[0].Should().Contain("\"risk\"");
    }

    [Fact]
    public void Data_table_prefers_personal_memory_until_a_server_view_is_explicitly_selected()
    {
        var script = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "js", "components", "data-table.js");

        script.Should().Contain("hasExplicitServerView");
        script.Should().Contain("resolveInitialColumnState");
        script.Should().Contain("resolveInitialRowDensity");
        script.Should().Contain("params.has(\"ViewPreset\")");
        script.Should().Contain("params.has(\"SavedViewId\")");
    }

    [Fact]
    public void Desktop_layout_defines_notebook_desktop_and_ultrawide_boundaries()
    {
        var css = FrontendAssetStructureTests.ReadCssLayers();

        css.Should().Contain("@media (min-width: 768px) and (max-width: 1199.98px)");
        css.Should().Contain("@media (min-width: 1600px)");
        css.Should().Contain("max-width: calc(100vw - 1.5rem);");
        css.Should().Contain("overscroll-behavior-inline: contain;");
    }

    [Fact]
    public void Sql_configuration_tools_cover_connection_migrations_health_and_release_launch()
    {
        var script = ReadRepositoryFile("SQL配置工具.ps1");
        var desktopTool = ReadRepositoryFile("tools", "SqlConfigTool", "Program.cs");

        script.Should().Contain("[switch]$ApplyMigrations");
        script.Should().Contain("[switch]$HealthCheck");
        script.Should().Contain("[switch]$Launch");
        script.Should().Contain("Invoke-DatabaseMigration");
        script.Should().Contain("Test-ProjectHealth");
        script.Should().Contain("Start-ProjectRelease");
        desktopTool.Should().Contain("測試連線");
        desktopTool.Should().Contain("套用資料庫更新");
        desktopTool.Should().Contain("檢查系統健康");
        desktopTool.Should().Contain("啟動系統");
        desktopTool.Should().Contain("RunDotNetAsync");
    }

    [Fact]
    public void Open_project_report_declares_the_approved_default_columns_and_presets()
    {
        var registry = ReadRepositoryFile("src", "ProjectManager.Web", "Services", "DataViews", "DataViewRegistry.cs");
        var table = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Reports", "OpenProjects", "_OpenProjectsTable.cshtml");

        registry.Should().Contain("Preset(\"risk\"");
        registry.Should().Contain("Preset(\"progress\"");
        registry.Should().Contain("Preset(\"finance\"");
        registry.Should().Contain("Preset(\"full\"");
        table.Should().Contain("data-column=\"projectNumber\"");
        table.Should().Contain("data-column=\"name\"");
        table.Should().Contain("data-column=\"assignments\"");
        table.Should().Contain("data-column=\"collectionPercent\"");
        table.Should().Contain("data-column=\"risk\"");
        table.Should().Contain("table-text-clamp");
    }

    [Fact]
    public void Long_running_forms_and_exports_expose_processing_feedback()
    {
        var feedback = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "js", "core", "feedback.js");
        var projectPage = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Index.cshtml");
        var maintenancePage = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "MaintenanceOrders", "Index.cshtml");
        var importPage = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Import.cshtml");
        var exchangePage = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "DataExchange", "Index.cshtml");

        feedback.Should().Contain("aria-busy");
        feedback.Should().Contain("data-processing-label");
        feedback.Should().Contain("pageshow");
        projectPage.Should().Contain("正在刪除…");
        maintenancePage.Should().Contain("正在刪除…");
        importPage.Should().Contain("正在匯入…");
        exchangePage.Should().Contain("正在準備匯出…");
    }

    [Fact]
    public void Legacy_project_type_migration_backfills_zero_values_to_engineering()
    {
        var migrationsDirectory = RepositoryPath("src", "ProjectManager.Web", "Migrations");
        var migration = Directory
            .GetFiles(migrationsDirectory, "*RepairLegacyProjectTypes.cs")
            .Should().ContainSingle().Subject;
        var source = File.ReadAllText(migration).ReplaceLineEndings("\n");

        source.Should().Contain("UPDATE [Projects] SET [ProjectType] = 2 WHERE [ProjectType] = 0");
    }

    [Fact]
    public void Desktop_shell_keeps_wide_tables_inside_the_content_viewport()
    {
        var css = FrontendAssetStructureTests.ReadCssLayers();

        css.Should().Contain("min-width: 0;");
        css.Should().Contain("max-width: calc(100vw - var(--sidebar-width));");
        css.Should().Contain("max-width: calc(100vw - var(--sidebar-collapsed-width));");
        css.Should().Contain(".data-table-wrap");
        css.Should().Contain("width: 100%;");
    }

    [Fact]
    public void Navigation_script_marks_the_current_module_and_opens_its_group()
    {
        var js = FrontendAssetStructureTests.ReadJavaScriptModules();

        js.Should().Contain("initActiveNavigation");
        js.Should().Contain("aria-current");
        js.Should().Contain("nav-group-open");
    }

    [Fact]
    public void Sidebar_navigation_has_business_report_and_system_sections_with_accessible_tooltips()
    {
        var layout = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_Layout.cshtml");
        var navigation = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_SidebarNavigation.cshtml");
        var script = FrontendAssetStructureTests.ReadJavaScriptModules();

        navigation.Should().Contain("業務工作");
        navigation.Should().Contain("報表分析");
        navigation.Should().Contain("系統管理");
        navigation.Should().Contain("data-nav-label");
        layout.Should().Contain("data-sidebar-toggle");
        layout.Should().Contain("aria-expanded=\"true\"");
        script.Should().Contain("aria-expanded");
        script.Should().Contain("Escape");
    }

    [Fact]
    public void Primary_data_pages_use_the_shared_actionable_empty_state()
    {
        var partial = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_EmptyState.cshtml");
        var projectPage = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Index.cshtml");
        var reportTable = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Reports", "OpenProjects", "_OpenProjectsTable.cshtml");
        var maintenancePage = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "MaintenanceOrders", "Index.cshtml");

        partial.Should().Contain("Model.Title");
        partial.Should().Contain("Model.Description");
        partial.Should().Contain("PrimaryActionText");
        partial.Should().Contain("ClearActionText");
        projectPage.Should().Contain("partial name=\"_EmptyState\"");
        reportTable.Should().Contain("partial name=\"_EmptyState\"");
        maintenancePage.Should().Contain("partial name=\"_EmptyState\"");
    }

    [Fact]
    public void Project_overview_keeps_analysis_optional_and_prioritizes_work_controls()
    {
        var page = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Index.cshtml");

        page.Should().Contain("<details class=\"project-analysis-panel data-work-surface\"");
        page.Should().Contain("<summary class=\"project-analysis-summary\"");
        page.IndexOf("data-filter-drawer", StringComparison.Ordinal)
            .Should().BeLessThan(page.IndexOf("project-analysis-panel", StringComparison.Ordinal));
    }

    [Fact]
    public void Project_filters_and_destructive_controls_have_specific_accessible_names()
    {
        var page = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Index.cshtml");

        page.Should().Contain("aria-label=\"搜尋專案名稱\"");
        page.Should().Contain("aria-label=\"篩選經辦人員\"");
        page.Should().Contain("aria-label=\"選擇全部專案\"");
        page.Should().Contain("aria-label=\"選擇專案 @project.ProjectNumber\"");
        page.Should().Contain("aria-label=\"刪除專案 @project.ProjectNumber\"");
    }

    [Fact]
    public void Clear_glass_theme_uses_opaque_surfaces_for_dense_work_areas()
    {
        var css = FrontendAssetStructureTests.ReadCssLayers();

        css.Should().Contain("body.theme-clear-glass .data-work-surface");
        css.Should().Contain("background: rgba(255, 255, 255, 0.86);");
        css.Should().Contain("font-variant-numeric: tabular-nums;");
    }

    [Fact]
    public void Dense_data_surfaces_use_restrained_elevation_and_are_not_tilt_targets()
    {
        var css = FrontendAssetStructureTests.ReadCssLayers();
        var script = FrontendAssetStructureTests.ReadJavaScriptModules();

        css.Should().Contain("--app-shadow-soft: 0 2px 8px rgba(15, 23, 42, 0.045);");
        css.Should().Contain(".data-work-surface,\n.data-list-card,\n.data-table-wrap");
        css.Should().Contain("box-shadow: 0 1px 3px rgba(15, 23, 42, 0.055);");
        script.Should().NotContain("hoverSelector = [\n    \".data-table-wrap\"");
        script.Should().NotContain("hoverSelector = [\n    \".data-list-card\"");
    }

    [Fact]
    public void Project_table_preserves_readable_widths_for_identity_and_people_columns()
    {
        var css = FrontendAssetStructureTests.ReadCssLayers();

        css.Should().Contain(".data-table [data-column=\"projectNumber\"]");
        css.Should().Contain(".data-table [data-column=\"assignments\"]");
        css.Should().Contain("min-width: 8.5rem;");
        css.Should().Contain("min-width: 10rem;");
    }

    [Fact]
    public void Project_details_load_only_the_selected_server_tab()
    {
        var adminPage = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Details.cshtml");
        var workbenchPage = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "Projects", "Details.cshtml");
        var adminModel = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Details.cshtml.cs");

        adminPage.Should().Contain("data-detail-tabs-server");
        workbenchPage.Should().Contain("data-detail-tabs-server");
        adminPage.Should().Contain("asp-route-Tab=\"gantt\"");
        adminPage.Should().Contain("@if (Model.ActiveTab == \"audit\")");
        adminModel.Should().Contain("if (ActiveTab == \"gantt\")");
        adminModel.Should().Contain("if (ActiveTab == \"audit\")");
        adminModel.Should().Contain("AsSplitQuery()");
    }

    [Fact]
    public void Audit_trail_supports_database_paging_and_user_selected_page_sizes()
    {
        var partial = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_AuditTrail.cshtml");
        var service = ReadRepositoryFile("src", "ProjectManager.Web", "Services", "AuditTrailQueryService.cs");

        partial.Should().Contain("name=\"AuditPageSize\"");
        partial.Should().Contain("操作記錄分頁");
        service.Should().Contain("AllowedPageSizes = [10, 25, 50, 100]");
        service.Should().Contain(".Skip((currentPage - 1) * selectedPageSize)");
        service.Should().Contain(".Take(selectedPageSize)");
    }

    [Fact]
    public void Gantt_exposes_progress_tooltips_and_sticky_desktop_headers()
    {
        var partial = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_ProjectGanttPanel.cshtml");
        var print = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "Projects", "GanttPrint.cshtml");
        var css = FrontendAssetStructureTests.ReadCssLayers();

        partial.Should().Contain("gantt-progress-marker");
        partial.Should().Contain("GetProgressSummary");
        print.Should().Contain("GetProgressSummary(progressPoint)");
        css.Should().Contain(".gantt-panel .gantt-chart-header");
        css.Should().Contain("position: sticky;");
        css.Should().Contain("content: attr(data-tooltip)");
    }

    [Fact]
    public void Maintenance_orders_match_template_fields_and_support_grouped_column_visibility()
    {
        var index = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "MaintenanceOrders", "Index.cshtml");
        var create = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "MaintenanceOrders", "Create.cshtml");
        var css = FrontendAssetStructureTests.ReadCssLayers();
        var script = FrontendAssetStructureTests.ReadJavaScriptModules();

        index.Should().Contain("maintenance-order-table");
        index.Should().Contain("data-column-group=\"remoteScope softwareScope hardwareScope\"");
        index.Should().Contain("data-column=\"description\"");
        index.Should().Contain("data-column-manager-table");
        create.Should().Contain("Input.ContractNumber");
        create.Should().Contain("Input.OnSiteSoftwareFrequency");
        create.Should().Contain("Input.MaintenanceDescription");
        css.Should().Contain(".maintenance-order-table");
        script.Should().Contain("data-column-group");
        script.Should().Contain("data-visible-column-count");
    }

    [Fact]
    public void Dynamic_project_values_can_opt_out_of_language_conversion()
    {
        var adminPage = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Details.cshtml");
        var converter = ReadRepositoryFile("src", "ProjectManager.Web", "Services", "HtmlLanguageConverter.cs");

        adminPage.Should().Contain("data-language-preserve");
        converter.Should().Contain("data-language-preserve");
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        return File.ReadAllText(RepositoryPath(pathParts)).ReplaceLineEndings("\n");
    }

    private static string RepositoryPath(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ProjectManager.sln")))
        {
            directory = directory.Parent;
        }

        var root = directory ?? throw new DirectoryNotFoundException("Cannot locate ProjectManager.sln.");
        return Path.Combine(new[] { root.FullName }.Concat(pathParts).ToArray());
    }
}
