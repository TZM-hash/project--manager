# 品質門禁、運維監控與任務進度中心設計規格

日期：2026-07-15

## 目標

建立可重複執行的可訪問性品質門禁、管理員運維狀態頁，以及具持久化任務記錄的匯入／匯出／批量操作進度中心。

## 品質門禁

- Playwright 覆蓋鍵盤 Tab／Enter／Space／Escape。
- 720px 有效寬度模擬 200% 縮放，頁面本體不得水平溢出，資料表允許容器內捲動。
- 主按鈕、狀態徽章、次要文字與 Focus Ring 執行 WCAG AA 對比檢查。
- 新增 PowerShell `scripts/quality-gate.ps1`，依序執行 JS 語法、Release build、完整 .NET 測試與 Playwright。

## 運維監控

- `/health/live`：程序存活。
- `/health/ready`：資料庫連線與必要目錄可寫。
- 管理員頁 `/Admin/Operations` 顯示：
  - 資料庫連線狀態；
  - SQL Server 最近完整備份時間；無 `msdb` 權限時顯示「無法讀取」而非報錯；
  - 應用資料磁碟總量、可用量與警示；
  - 最近例外時間與近 24 小時數量；
  - 背景 Worker 心跳與待處理任務數。
- 新增例外記錄 Middleware，以 UTF-8 JSON Lines 寫入 `App_Data/logs`，每日一檔並限制讀取筆數。

## 任務進度中心

### OperationJob

- `Id Guid`、`UserId`、`Type`、`Status`、`ProgressPercent`、`Summary`、`ErrorDetailsJson`、`InputJson`、`OutputPath`、`CreatedAt`、`StartedAt`、`CompletedAt`、`UpdatedAt`、`RowVersion`。
- 狀態：Queued、Running、Succeeded、Failed、Cancelled。
- 使用者只能查看自己的任務；管理員可查看全部。

### Worker

- `BackgroundService` 從資料庫以建立時間取得 Queued 任務。
- 以原子狀態更新避免同一任務被重複處理。
- 每個任務階段更新進度與摘要；程序重啟時 Running 任務重設為 Queued 並重新執行。
- 輸出檔保存於 `App_Data/operations/{jobId}`，下載後仍保留到清理期限。

### 第一階段整合

- 全量資料匯出。
- 全量資料匯入（上傳先保存到工作目錄）。
- 專案批量刪除。
- 保養訂單批量刪除。

頁面提交後立即回到任務中心；前端每 2 秒查詢狀態，顯示真實百分比、完成摘要、錯誤明細與下載按鈕。

## 安全與清理

- 輸入／輸出路徑必須位於 `App_Data/operations`。
- 下載 Handler 驗證任務擁有者與路徑。
- 錯誤明細限制長度，不將堆疊直接顯示給一般使用者。
- 提供 30 天完成任務與輸出檔清理服務。

## Migration

- 新增 `OperationJobs` 表及使用者、狀態、建立時間索引。
- 不修改既有業務表。

## 測試

- 健康檢查與監控狀態降級。
- 例外日志 UTF-8、路徑限制與統計。
- 任務狀態轉換、跨使用者隔離、失敗報告、重啟恢復。
- 四種操作建立任務並產生結果。
- 進度中心鍵盤、縮放與對比測試。

## Git 交付

本批使用獨立提交，包含 Migration、Worker、監控頁、健康端點、品質腳本、測試及 `docs/2026-07-15-第五批运维任务中心说明.md`。
