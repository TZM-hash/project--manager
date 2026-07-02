/*
    ProjectManager SQL 数据字典查询脚本

    使用方式：
    1. 连接到 ProjectManager 数据库。
    2. 执行本脚本。
    3. 结果集依次为：表清单、字段清单、外键关系、索引清单。
*/

SET NOCOUNT ON;

DECLARE @BusinessTables TABLE
(
    TableName sysname PRIMARY KEY,
    ChineseName nvarchar(100) NOT NULL,
    Description nvarchar(500) NOT NULL
);

INSERT INTO @BusinessTables (TableName, ChineseName, Description)
VALUES
    (N'AspNetUsers', N'用户表', N'Identity 用户基础字段，并扩展显示名称、启用状态、创建时间。'),
    (N'Projects', N'项目主表', N'项目主数据，包含工号、金额、进度、状态、结案年月等。'),
    (N'ProjectAssignments', N'项目人员表', N'项目与专案人员的关系。'),
    (N'ProjectStatuses', N'项目状态表', N'项目流程状态字典。'),
    (N'ProjectStatusStyles', N'状态样式表', N'状态在页面上的颜色和加粗配置。'),
    (N'PurchaseRequests', N'请购记录表', N'项目下的内购/外购记录、金额、付款比例、实付金额。'),
    (N'MonthlySettlementBatches', N'月结批次表', N'每次生成月结报表的批次头。'),
    (N'MonthlySettlementItems', N'月结明细表', N'月结时从项目、人员、请购汇总出的快照明细。'),
    (N'AuditLogs', N'操作日志表', N'记录项目新增、修改、删除、进度更新等留痕。');

DECLARE @ColumnDescriptions TABLE
(
    TableName sysname NOT NULL,
    ColumnName sysname NOT NULL,
    Description nvarchar(500) NOT NULL,
    PRIMARY KEY (TableName, ColumnName)
);

INSERT INTO @ColumnDescriptions (TableName, ColumnName, Description)
VALUES
    (N'Projects', N'Year', N'项目年度，与项目工号组成未删除项目唯一键。'),
    (N'Projects', N'ParentCaseNumber', N'母案案号。'),
    (N'Projects', N'ProjectNumber', N'项目工号。'),
    (N'Projects', N'Name', N'项目名称。'),
    (N'Projects', N'ProgressPercent', N'项目进度百分比。'),
    (N'Projects', N'ProjectAmount', N'项目金额。'),
    (N'Projects', N'CollectionPercent', N'收款比例。'),
    (N'Projects', N'ProgressDescription', N'进度说明。'),
    (N'Projects', N'StatusId', N'项目状态外键。'),
    (N'Projects', N'UpdatedByUserId', N'最近更新人。'),
    (N'Projects', N'ClosedYearMonth', N'结案年月，保存为当月 1 日。'),
    (N'Projects', N'IsDeleted', N'软删除标记。'),
    (N'ProjectAssignments', N'ProjectId', N'项目外键。'),
    (N'ProjectAssignments', N'UserId', N'用户外键。'),
    (N'ProjectAssignments', N'RoleInProject', N'项目内角色文本。'),
    (N'ProjectStatuses', N'Code', N'状态编码，唯一。'),
    (N'ProjectStatuses', N'Name', N'状态名称。'),
    (N'ProjectStatuses', N'SortOrder', N'展示排序。'),
    (N'ProjectStatuses', N'IsClosed', N'是否代表结案状态。'),
    (N'ProjectStatuses', N'IsActive', N'是否可继续选择。'),
    (N'ProjectStatusStyles', N'StatusId', N'状态外键，一对一。'),
    (N'ProjectStatusStyles', N'TextColor', N'状态徽标文字颜色。'),
    (N'ProjectStatusStyles', N'BackgroundColor', N'状态徽标背景颜色。'),
    (N'ProjectStatusStyles', N'IsBold', N'是否加粗显示。'),
    (N'PurchaseRequests', N'ProjectId', N'所属项目外键。'),
    (N'PurchaseRequests', N'RequestNumber', N'请购号。'),
    (N'PurchaseRequests', N'PurchaseType', N'请购类型：1 内购，2 外购。'),
    (N'PurchaseRequests', N'PurchaseStaffUserId', N'请购人员。'),
    (N'PurchaseRequests', N'PurchaseAmount', N'请购金额。'),
    (N'PurchaseRequests', N'SubCaseContactUserId', N'子案对接人员。'),
    (N'PurchaseRequests', N'PaymentPercent', N'付款比例。'),
    (N'PurchaseRequests', N'ActualPaidAmount', N'实际已付款金额。'),
    (N'PurchaseRequests', N'IsDeleted', N'软删除标记。'),
    (N'MonthlySettlementBatches', N'Year', N'月结年度。'),
    (N'MonthlySettlementBatches', N'Month', N'月结月份。'),
    (N'MonthlySettlementBatches', N'BatchNumber', N'同年同月内递增批次号。'),
    (N'MonthlySettlementBatches', N'CreatedByUserId', N'创建人。'),
    (N'MonthlySettlementItems', N'BatchId', N'月结批次外键。'),
    (N'MonthlySettlementItems', N'ProjectId', N'来源项目 ID。'),
    (N'MonthlySettlementItems', N'ProjectPersonnelText', N'月结时项目人员文本快照。'),
    (N'MonthlySettlementItems', N'PurchaseRequestSummary', N'请购号汇总。'),
    (N'MonthlySettlementItems', N'PurchaseAmountTotal', N'请购金额合计。'),
    (N'MonthlySettlementItems', N'ActualPaidAmountTotal', N'实际已付款合计。'),
    (N'MonthlySettlementItems', N'SourceUpdatedAt', N'来源项目最近更新时间快照。'),
    (N'AuditLogs', N'UserId', N'操作人。'),
    (N'AuditLogs', N'Action', N'操作类型。'),
    (N'AuditLogs', N'EntityName', N'被操作实体名称。'),
    (N'AuditLogs', N'EntityId', N'被操作实体 ID。'),
    (N'AuditLogs', N'ProjectId', N'项目 ID，用于按项目查询留痕。'),
    (N'AuditLogs', N'ProjectNumber', N'项目工号快照。'),
    (N'AuditLogs', N'ChangeSummary', N'人可读变更摘要。'),
    (N'AuditLogs', N'ChangeDetailsJson', N'字段级和请购明细级变更 JSON。');

SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    COALESCE(bt.ChineseName, N'') AS ChineseName,
    COALESCE(bt.Description, N'') AS Description,
    SUM(p.rows) AS RowCountApprox
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
LEFT JOIN sys.partitions AS p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
LEFT JOIN @BusinessTables AS bt ON bt.TableName = t.name
WHERE t.is_ms_shipped = 0
GROUP BY s.name, t.name, bt.ChineseName, bt.Description
ORDER BY s.name, t.name;

SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    c.column_id AS ColumnOrder,
    c.name AS ColumnName,
    ty.name AS DataType,
    CASE
        WHEN ty.name IN (N'nvarchar', N'nchar') AND c.max_length > 0 THEN c.max_length / 2
        WHEN ty.name IN (N'varchar', N'char', N'varbinary', N'binary') THEN c.max_length
        ELSE NULL
    END AS MaxLength,
    c.precision AS NumericPrecision,
    c.scale AS NumericScale,
    c.is_nullable AS IsNullable,
    CASE WHEN pk.column_id IS NULL THEN 0 ELSE 1 END AS IsPrimaryKey,
    dc.definition AS DefaultValue,
    COALESCE(cd.Description, N'') AS Description
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.columns AS c ON c.object_id = t.object_id
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints AS dc
    ON dc.parent_object_id = t.object_id
    AND dc.parent_column_id = c.column_id
LEFT JOIN
(
    SELECT ic.object_id, ic.column_id
    FROM sys.indexes AS i
    JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    WHERE i.is_primary_key = 1
) AS pk ON pk.object_id = t.object_id AND pk.column_id = c.column_id
LEFT JOIN @ColumnDescriptions AS cd
    ON cd.TableName = t.name
    AND cd.ColumnName = c.name
WHERE t.is_ms_shipped = 0
ORDER BY s.name, t.name, c.column_id;

SELECT
    fk.name AS ForeignKeyName,
    OBJECT_SCHEMA_NAME(fk.parent_object_id) AS ChildSchema,
    OBJECT_NAME(fk.parent_object_id) AS ChildTable,
    c1.name AS ChildColumn,
    OBJECT_SCHEMA_NAME(fk.referenced_object_id) AS ParentSchema,
    OBJECT_NAME(fk.referenced_object_id) AS ParentTable,
    c2.name AS ParentColumn,
    fk.delete_referential_action_desc AS DeleteAction
FROM sys.foreign_keys AS fk
JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns AS c1 ON c1.object_id = fkc.parent_object_id AND c1.column_id = fkc.parent_column_id
JOIN sys.columns AS c2 ON c2.object_id = fkc.referenced_object_id AND c2.column_id = fkc.referenced_column_id
ORDER BY ChildTable, ForeignKeyName, fkc.constraint_column_id;

SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    i.name AS IndexName,
    i.is_unique AS IsUnique,
    i.has_filter AS HasFilter,
    i.filter_definition AS FilterDefinition,
    STRING_AGG(c.name, N', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS KeyColumns
FROM sys.indexes AS i
JOIN sys.tables AS t ON t.object_id = i.object_id
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE t.is_ms_shipped = 0
  AND i.name IS NOT NULL
GROUP BY s.name, t.name, i.name, i.is_unique, i.has_filter, i.filter_definition
ORDER BY s.name, t.name, i.name;
