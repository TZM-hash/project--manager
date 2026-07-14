# 甘特、協作記錄與並發保護設計規格

日期：2026-07-15

## 目標

擴充甘特任務的里程碑、負責人、前置依賴、計畫／實際日期與逾期判斷；新增專案協作記錄；使用 SQL Server `rowversion` 防止後提交覆蓋先提交。

## 資料模型

### Project

- 新增 `RowVersion byte[]`，設定為 RowVersion／ConcurrencyToken。
- 專案編輯與進度更新表單回傳 Base64 RowVersion。

### ProjectGanttPlan

- 新增 `RowVersion byte[]`，作為整份甘特資料的聚合並發權杖。
- 儲存時先比對權杖，再更新計畫與任務；衝突時不寫入任何資料。

### ProjectGanttTask

- 新增 `IsMilestone`。
- 新增 `AssigneeUserId` 外鍵。
- 新增 `DependsOnTaskId` 自我外鍵；一個任務可有一個直接前置任務，透過鏈形成依賴關係。
- 保留 `PlannedStartDate`、`PlannedFinishDate`。
- 新增 `ActualStartDate`、`ActualFinishDate`。
- 逾期：計畫完成日早於今天，尚未實際完成且進度小於 100%。

### ProjectCollaborationRecord

- `Id`、`ProjectId`、`Content`、`CreatedByUserId`、`UpdatedByUserId`、`CreatedAt`、`UpdatedAt`、`RowVersion`。
- 內容為純文字／經既有 Sanitizer 正規化的安全文字，不接受任意腳本。
- 專案刪除時級聯刪除；使用者外鍵使用 Restrict／SetNull，保留歷史。

## 操作流程

- 甘特任務以目前列的持久化 ID 選擇前置任務；新任務第一次儲存後即可成為其他任務的前置任務。
- 依賴不得指向自己，不得形成循環；刪除被依賴任務時清除後繼任務的直接依賴。
- 里程碑使用單一日期；若開始／完成不同，驗證失敗。
- 實際完成日存在時自動視為 100% 顯示狀態，但不靜默覆寫專案總進度。
- 協作記錄在專案詳情頁以時間線顯示，可新增、編輯自己的記錄；管理員可編輯／刪除全部。

## 並發衝突

- 捕捉 `DbUpdateConcurrencyException`。
- 頁面顯示繁體中文衝突訊息：「資料已由其他使用者更新，請重新載入後合併變更。」
- 不自動覆蓋資料；提供重新載入連結。
- 甘特以 Plan RowVersion 保護整個聚合；專案與協作記錄使用各自 RowVersion。

## Migration

單一新增 Migration：

- 修改 `Projects`、`ProjectGanttPlans`、`ProjectGanttTasks`。
- 新增 `ProjectCollaborationRecords`、索引與外鍵。
- 不刪除既有欄位；新布林／RowVersion 欄位使用安全預設。

## 測試

- 里程碑、日期、依賴、自我依賴與循環驗證。
- 甘特與專案並發衝突會拒絕第二次提交。
- 協作記錄權限與並發衝突。
- Migration 僅包含核准的新增欄位／資料表。
- 視覺與鍵盤測試覆蓋逾期、里程碑、負責人與依賴控制。

## Git 交付

本批使用獨立提交，包含 Migration、模型、服務、UI、測試及 `docs/2026-07-15-第四批甘特并发说明.md`。
