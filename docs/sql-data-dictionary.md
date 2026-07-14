# 项目管理系统 SQL 数据字典

本文档描述 `ProjectManager` 数据库的主要业务表、字段含义、关系和维护注意事项。系统同时使用 ASP.NET Core Identity 的内置表保存账号、角色和登录信息。

## 表清单

| 表名 | 中文名称 | 说明 |
| --- | --- | --- |
| `AspNetUsers` | 用户表 | Identity 用户基础字段，并扩展显示名称、启用状态、创建时间。 |
| `AspNetRoles` | 角色表 | Identity 角色。系统角色常量见 `RoleNames`。 |
| `AspNetUserRoles` | 用户角色关系表 | 用户与角色多对多关系。 |
| `Projects` | 项目主表 | 项目主数据，包含工号、金额、进度、状态、结案年月等。 |
| `ProjectAssignments` | 项目人员表 | 项目与专案人员的关系。 |
| `ProjectStatuses` | 项目状态表 | 项目流程状态字典，如已立案、已请购、已结案。 |
| `ProjectStatusStyles` | 状态样式表 | 状态在页面上的文字色、背景色和加粗配置。 |
| `PurchaseRequests` | 请购记录表 | 项目下的内购/外购记录、金额、付款比例、实付金额。 |
| `MonthlySettlementBatches` | 月结批次表 | 每次生成月结报表的批次头。 |
| `MonthlySettlementItems` | 月结明细表 | 月结时从项目、人员、请购汇总出的快照明细。 |
| `AuditLogs` | 操作日志表 | 记录项目新增、修改、删除、进度更新等留痕。 |
| `SavedDataViews` | 個人資料檢視表 | 保存使用者在指定資料頁面的篩選、欄位、行距與預設檢視。 |
| `ProjectGanttPlans` | 專案甘特計畫表 | 保存整體日期、說明與聚合並發版本。 |
| `ProjectGanttTasks` | 甘特工作表 | 保存里程碑、負責人、前置工作、計畫／實際日期與進度。 |
| `ProjectCollaborationRecords` | 專案協作記錄表 | 保存專案時間線上的進度、風險、決議與待辦記錄。 |
| `OperationJobs` | 背景工作表 | 保存匯入、匯出與批量刪除工作的狀態、進度、結果及檔案位置。 |

## Projects 项目主表

| 字段 | 类型 | 允许空 | 说明 |
| --- | --- | --- | --- |
| `Id` | `int` | 否 | 主键。 |
| `Year` | `int` | 否 | 项目年度。与 `ProjectNumber` 组成唯一业务键。 |
| `ParentCaseNumber` | `nvarchar(64)` | 是 | 母案案号。 |
| `ProjectNumber` | `nvarchar(64)` | 否 | 项目工号。同一年度未删除项目内唯一。 |
| `Name` | `nvarchar(200)` | 否 | 项目名称。 |
| `ProgressPercent` | `decimal(5,2)` | 否 | 项目进度百分比，范围由业务规则校验。 |
| `ProjectAmount` | `decimal(18,2)` | 否 | 项目金额。 |
| `CollectionPercent` | `decimal(5,2)` | 否 | 收款比例。 |
| `ProgressDescription` | `nvarchar(max)` | 是 | 进度说明。 |
| `StatusId` | `int` | 否 | 外键，关联 `ProjectStatuses.Id`。 |
| `UpdatedByUserId` | `nvarchar(450)` | 是 | 最近更新人，关联 `AspNetUsers.Id`。 |
| `ClosedYearMonth` | `date` | 是 | 结案年月，统一保存为当月 1 日。 |
| `UpdatedAt` | `datetimeoffset` | 否 | 最近更新时间。 |
| `CreatedAt` | `datetimeoffset` | 否 | 创建时间。 |
| `IsDeleted` | `bit` | 否 | 软删除标记。 |
| `RowVersion` | `rowversion` | 否 | 樂觀並發權杖，避免後提交覆蓋先提交。 |

关键索引：`IX_Projects_Year_ProjectNumber`，唯一索引，过滤条件为 `IsDeleted = 0`。

## ProjectGanttPlans / ProjectGanttTasks 甘特資料

`ProjectGanttPlans` 以 `ProjectId` 與專案一對一，`RowVersion` 保護整份甘特聚合。`ProjectGanttTasks` 增加 `IsMilestone`、`OwnerUserId`、`PredecessorTaskId`、`ActualStartDate`、`ActualFinishDate`；前置工作使用同表自我關聯且刪除行為為 Restrict。

## ProjectCollaborationRecords 專案協作記錄表

| 欄位 | 類型 | 允許空 | 說明 |
| --- | --- | --- | --- |
| `Id` | `int` | 否 | 主鍵。 |
| `ProjectId` | `int` | 否 | 所屬專案。 |
| `Category` | `nvarchar(50)` | 否 | 進度協作、風險、決議、待辦等類別。 |
| `Content` | `nvarchar(4000)` | 否 | 純文字協作內容。 |
| `CreatedByUserId` | `nvarchar(450)` | 否 | 建立人。 |
| `CreatedAt` / `UpdatedAt` | `datetimeoffset` | 否 | 建立與更新時間。 |
| `RowVersion` | `rowversion` | 否 | 編輯／刪除並發權杖。 |

關鍵索引：`ProjectId + CreatedAt`，用於專案詳情時間線查詢。

## ProjectAssignments 项目人员表

| 字段 | 类型 | 允许空 | 说明 |
| --- | --- | --- | --- |
| `Id` | `int` | 否 | 主键。 |
| `ProjectId` | `int` | 否 | 外键，关联 `Projects.Id`。 |
| `UserId` | `nvarchar(450)` | 否 | 外键，关联 `AspNetUsers.Id`。 |
| `RoleInProject` | `nvarchar(80)` | 否 | 项目内角色文本，目前主要为专案人员。 |

## ProjectStatuses 项目状态表

| 字段 | 类型 | 允许空 | 说明 |
| --- | --- | --- | --- |
| `Id` | `int` | 否 | 主键。 |
| `Code` | `nvarchar(64)` | 否 | 状态编码，唯一。 |
| `Name` | `nvarchar(80)` | 否 | 状态名称。 |
| `SortOrder` | `int` | 否 | 页面展示和流程时间线排序。 |
| `IsClosed` | `bit` | 否 | 是否代表结案状态。 |
| `IsActive` | `bit` | 否 | 是否可继续选择。 |

## ProjectStatusStyles 状态样式表

| 字段 | 类型 | 允许空 | 说明 |
| --- | --- | --- | --- |
| `Id` | `int` | 否 | 主键。 |
| `StatusId` | `int` | 否 | 外键，关联 `ProjectStatuses.Id`，一对一。 |
| `TextColor` | `nvarchar(16)` | 否 | 状态徽标文字颜色。 |
| `BackgroundColor` | `nvarchar(16)` | 否 | 状态徽标背景颜色。 |
| `IsBold` | `bit` | 否 | 是否加粗显示。 |

## PurchaseRequests 请购记录表

| 字段 | 类型 | 允许空 | 说明 |
| --- | --- | --- | --- |
| `Id` | `int` | 否 | 主键。 |
| `ProjectId` | `int` | 否 | 外键，关联 `Projects.Id`。 |
| `RequestNumber` | `nvarchar(64)` | 否 | 请购号。 |
| `PurchaseType` | `int` | 否 | 请购类型：`1` 内购，`2` 外购。 |
| `PurchaseStaffUserId` | `nvarchar(450)` | 是 | 请购人员，关联 `AspNetUsers.Id`。 |
| `PurchaseAmount` | `decimal(18,2)` | 否 | 请购金额。 |
| `SubCaseContactUserId` | `nvarchar(450)` | 是 | 子案对接人员，关联 `AspNetUsers.Id`。 |
| `PaymentPercent` | `decimal(5,2)` | 否 | 付款比例。 |
| `ActualPaidAmount` | `decimal(18,2)` | 否 | 实际已付款金额。 |
| `Notes` | `nvarchar(max)` | 是 | 备注。 |
| `CreatedAt` | `datetimeoffset` | 否 | 创建时间。 |
| `UpdatedAt` | `datetimeoffset` | 否 | 更新时间。 |
| `IsDeleted` | `bit` | 否 | 软删除标记。 |

## MonthlySettlementBatches 月结批次表

| 字段 | 类型 | 允许空 | 说明 |
| --- | --- | --- | --- |
| `Id` | `int` | 否 | 主键。 |
| `Year` | `int` | 否 | 月结年度。 |
| `Month` | `int` | 否 | 月结月份。 |
| `BatchNumber` | `int` | 否 | 同年同月内递增批次号。 |
| `CreatedByUserId` | `nvarchar(450)` | 否 | 创建人，关联 `AspNetUsers.Id`。 |
| `CreatedAt` | `datetimeoffset` | 否 | 创建时间。 |
| `Notes` | `nvarchar(max)` | 是 | 批次备注。 |

关键索引：`Year + Month + BatchNumber` 唯一。

## MonthlySettlementItems 月结明细表

| 字段 | 类型 | 允许空 | 说明 |
| --- | --- | --- | --- |
| `Id` | `int` | 否 | 主键。 |
| `BatchId` | `int` | 否 | 外键，关联 `MonthlySettlementBatches.Id`。 |
| `ProjectId` | `int` | 否 | 来源项目 ID。 |
| `ParentCaseNumber` | `nvarchar(64)` | 是 | 来源项目母案案号。 |
| `ProjectNumber` | `nvarchar(64)` | 否 | 来源项目工号。 |
| `ProjectName` | `nvarchar(200)` | 否 | 来源项目名称。 |
| `ProjectPersonnelText` | `nvarchar(max)` | 否 | 月结时项目人员文本快照。 |
| `ProgressPercent` | `decimal(5,2)` | 否 | 月结时项目进度。 |
| `ProjectAmount` | `decimal(18,2)` | 否 | 月结时项目金额。 |
| `CollectionPercent` | `decimal(5,2)` | 否 | 月结时收款比例。 |
| `StatusName` | `nvarchar(max)` | 否 | 月结时状态名称。 |
| `IsClosed` | `bit` | 否 | 月结时是否结案。 |
| `ClosedYearMonth` | `date` | 是 | 月结时结案年月。 |
| `PurchaseRequestSummary` | `nvarchar(max)` | 否 | 请购号汇总。 |
| `PurchaseAmountTotal` | `decimal(18,2)` | 否 | 请购金额合计。 |
| `SubCaseContactSummary` | `nvarchar(max)` | 否 | 子案对接人员汇总。 |
| `PaymentPercentSummary` | `nvarchar(max)` | 否 | 付款比例汇总。 |
| `ActualPaidAmountTotal` | `decimal(18,2)` | 否 | 实际已付款合计。 |
| `ProgressDescription` | `nvarchar(max)` | 是 | 月结时进度说明。 |
| `UpdatedByUserName` | `nvarchar(max)` | 否 | 来源项目最近更新人名称快照。 |
| `SourceUpdatedAt` | `datetimeoffset` | 否 | 来源项目最近更新时间快照。 |

## AuditLogs 操作日志表

| 字段 | 类型 | 允许空 | 说明 |
| --- | --- | --- | --- |
| `Id` | `int` | 否 | 主键。 |
| `UserId` | `nvarchar(450)` | 是 | 操作人，关联 `AspNetUsers.Id`。 |
| `Action` | `nvarchar(80)` | 否 | 操作类型，如 `Create`、`Update`、`Delete`、`ProgressUpdate`。 |
| `EntityName` | `nvarchar(120)` | 否 | 被操作实体名称。项目留痕固定为 `Project`。 |
| `EntityId` | `nvarchar(120)` | 否 | 被操作实体 ID，兼容通用审计。 |
| `Description` | `nvarchar(max)` | 否 | 通用描述。 |
| `ProjectId` | `int` | 是 | 项目 ID，便于详情页按项目查询操作记录。 |
| `ProjectNumber` | `nvarchar(64)` | 是 | 项目工号快照。 |
| `ChangeSummary` | `nvarchar(500)` | 是 | 人可读变更摘要。 |
| `ChangeDetailsJson` | `nvarchar(max)` | 是 | 字段级和请购明细级变更 JSON。 |
| `CreatedAt` | `datetimeoffset` | 否 | 操作时间。 |

## 维护注意事项

## SavedDataViews 個人資料檢視表

| 欄位 | 類型 | 允許空 | 說明 |
| --- | --- | --- | --- |
| `Id` | `int` | 否 | 主鍵。 |
| `UserId` | `nvarchar(450)` | 否 | 擁有者，外鍵關聯 `AspNetUsers.Id`；使用者刪除時級聯刪除。 |
| `PageKey` | `nvarchar(80)` | 否 | 白名單頁面鍵。 |
| `Name` | `nvarchar(80)` | 否 | 個人檢視名稱。 |
| `FilterJson` | `nvarchar(8000)` | 否 | 經頁面白名單過濾後的篩選 JSON。 |
| `ColumnJson` | `nvarchar(8000)` | 否 | 經欄位白名單正規化後的可見欄位順序。 |
| `RowDensity` | `int` | 否 | `1` 緊湊、`2` 一般、`3` 寬鬆。 |
| `IsDefault` | `bit` | 否 | 是否為使用者在該頁面的預設檢視。 |
| `CreatedAt` | `datetimeoffset` | 否 | 建立時間。 |
| `UpdatedAt` | `datetimeoffset` | 否 | 最後更新時間。 |

索引：`(UserId, PageKey, Name)` 唯一；`(UserId, PageKey, IsDefault)` 用於讀取預設檢視，預設唯一性由服務層交易保證。

## OperationJobs 背景工作表

| 欄位 | 類型 | 允許空 | 說明 |
| --- | --- | --- | --- |
| `Id` | `int` | 否 | 主鍵。 |
| `Type` | `int` | 否 | 工作類型：全量匯出、全量匯入、專案批量刪除或保養訂單批量刪除。 |
| `Status` | `int` | 否 | 工作狀態：排隊、執行中、成功、失敗或取消。 |
| `RequestedByUserId` | `nvarchar(450)` | 否 | 建立工作者，外鍵關聯 `AspNetUsers.Id`，刪除行為為 Restrict。 |
| `PayloadJson` | `nvarchar(max)` | 是 | 工作輸入參數；批量操作保存目標識別資料。 |
| `ProgressPercent` | `int` | 否 | 0 至 100 的工作進度。 |
| `StatusMessage` | `nvarchar(500)` | 是 | 目前步驟的使用者可讀說明。 |
| `ResultSummary` | `nvarchar(2000)` | 是 | 完成後的成功／失敗數量與結果摘要。 |
| `ErrorDetails` | `nvarchar(max)` | 是 | 失敗明細或未處理例外內容。 |
| `InputRelativePath` | `nvarchar(500)` | 是 | `App_Data/operations` 下的匯入檔案相對路徑。 |
| `OutputRelativePath` | `nvarchar(500)` | 是 | `App_Data/operations` 下的輸出檔案相對路徑。 |
| `OutputFileName` | `nvarchar(260)` | 是 | 下載時顯示的原始檔名。 |
| `OutputContentType` | `nvarchar(160)` | 是 | 下載內容類型。 |
| `CreatedAt` / `UpdatedAt` | `datetimeoffset` | 否 | 建立與最後更新時間。 |
| `StartedAt` / `CompletedAt` | `datetimeoffset` | 是 | 開始與結束時間。 |
| `RowVersion` | `rowversion` | 否 | 背景工作狀態更新的樂觀並發版本。 |

索引：`(RequestedByUserId, CreatedAt)` 支援個人工作清單；`(Status, CreatedAt)` 支援背景 Worker 依狀態領取工作。

- 项目删除和请购删除均为软删除，查询业务数据时需要过滤 `IsDeleted = 0`。
- 月结明细是快照表，生成后不随项目后续修改自动变化。
- 审计明细 JSON 由 `AuditChangeDetail` 序列化生成，页面展示由 `AuditLogDisplayModel` 解析。
- 若新增业务表，请同步更新本文档和 `scripts/sql-data-dictionary.sql`。
