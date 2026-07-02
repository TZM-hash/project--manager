-- ==========================================================================
-- 台塑电子项目管理系统 - 数据库表结构字典
-- 生成日期: 2026-07-02
-- 数据库: SQL Server
-- 说明: 本文件包含系统所有业务表的建表 SQL 及字段说明
-- ==========================================================================

-- --------------------------------------------------------------------------
-- 1. 用户表 (AspNetUsers)
--    继承自 ASP.NET Core Identity 的 IdentityUser，扩展业务字段
-- --------------------------------------------------------------------------
-- 注：AspNetUsers 表由 EF Core Identity 自动创建，以下为扩展字段说明
-- 字段说明：
--   Id              nvarchar(450)   主键
--   UserName        nvarchar(256)   登录账号
--   Email           nvarchar(256)   邮箱（弱管理账号可不填）
--   DisplayName     nvarchar(256)   显示名称（中文姓名或昵称）
--   IsActive        bit             是否启用
--   CreatedAt       datetimeoffset  账号创建时间
--   IsWeakManaged   bit             是否为弱管理账号（不强制密码/邮箱）
-- --------------------------------------------------------------------------

-- --------------------------------------------------------------------------
-- 2. 角色表 (AspNetRoles)
--    由 EF Core Identity 自动创建
-- --------------------------------------------------------------------------
-- 预置角色:
--   Administrator   系统管理员
--   ProjectStaff    项目人员
--   Leader          领导
--   Viewer          查询人员
--   SubCaseContact  子案对接人
-- --------------------------------------------------------------------------

-- --------------------------------------------------------------------------
-- 3. 项目主数据表 (Projects)
-- --------------------------------------------------------------------------
CREATE TABLE [Projects] (
    [Id                  ] INT             IDENTITY(1,1) NOT NULL,
    [Year                ] INT             NOT NULL,           -- 项目年度
    [ParentCaseNumber    ] NVARCHAR(64)    NULL,               -- 母案案号
    [ProjectNumber       ] NVARCHAR(64)    NOT NULL,           -- 项目工号
    [Name                ] NVARCHAR(200)   NOT NULL,           -- 项目名称
    [ProgressPercent     ] DECIMAL(5,2)    NOT NULL DEFAULT 0, -- 项目进度百分比
    [ProjectAmount       ] DECIMAL(18,2)   NOT NULL DEFAULT 0, -- 项目金额
    [CollectionPercent   ] DECIMAL(5,2)    NOT NULL DEFAULT 0, -- 收款比例百分比
    [ProgressDescription ] NVARCHAR(MAX)   NULL,               -- 进度补充说明
    [StatusId            ] INT             NOT NULL,           -- 状态外键
    [UpdatedByUserId     ] NVARCHAR(450)   NULL,               -- 最近更新人
    [ClosedYearMonth     ] DATE            NULL,               -- 结案年月
    [UpdatedAt           ] DATETIMEOFFSET  NOT NULL,           -- 最近更新时间
    [CreatedAt           ] DATETIMEOFFSET  NOT NULL,           -- 创建时间
    [IsDeleted           ] BIT             NOT NULL DEFAULT 0, -- 软删除标记
    CONSTRAINT [PK_Projects] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Projects_ProjectStatuses_StatusId]
        FOREIGN KEY ([StatusId]) REFERENCES [ProjectStatuses] ([Id]),
    CONSTRAINT [FK_Projects_AspNetUsers_UpdatedByUserId]
        FOREIGN KEY ([UpdatedByUserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [UQ_Projects_Year_ProjectNumber]
        UNIQUE ([Year], [ProjectNumber]) WHERE ([IsDeleted] = 0)
);

-- --------------------------------------------------------------------------
-- 4. 项目人员分配表 (ProjectAssignments)
-- --------------------------------------------------------------------------
CREATE TABLE [ProjectAssignments] (
    [Id            ] INT             IDENTITY(1,1) NOT NULL,
    [ProjectId     ] INT             NOT NULL,           -- 项目 ID
    [UserId        ] NVARCHAR(450)   NOT NULL,           -- 人员用户 ID
    [RoleInProject ] NVARCHAR(80)    NOT NULL,           -- 项目内角色
    CONSTRAINT [PK_ProjectAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProjectAssignments_Projects_ProjectId]
        FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]),
    CONSTRAINT [FK_ProjectAssignments_AspNetUsers_UserId]
        FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id])
);

-- --------------------------------------------------------------------------
-- 5. 项目状态字典表 (ProjectStatuses)
-- --------------------------------------------------------------------------
CREATE TABLE [ProjectStatuses] (
    [Id        ] INT             IDENTITY(1,1) NOT NULL,
    [Code      ] NVARCHAR(64)    NOT NULL,           -- 状态编码
    [Name      ] NVARCHAR(80)    NOT NULL,           -- 状态名称
    [SortOrder ] INT             NOT NULL DEFAULT 0, -- 排序
    [IsClosed  ] BIT             NOT NULL DEFAULT 0, -- 是否结案状态
    [IsActive  ] BIT             NOT NULL DEFAULT 1, -- 是否启用
    CONSTRAINT [PK_ProjectStatuses] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_ProjectStatuses_Code] UNIQUE ([Code])
);

-- --------------------------------------------------------------------------
-- 6. 项目状态样式表 (ProjectStatusStyles)
-- --------------------------------------------------------------------------
CREATE TABLE [ProjectStatusStyles] (
    [Id             ] INT             IDENTITY(1,1) NOT NULL,
    [StatusId       ] INT             NOT NULL,           -- 状态 ID（一对一）
    [TextColor      ] NVARCHAR(16)    NOT NULL,           -- 文字颜色
    [BackgroundColor] NVARCHAR(16)    NOT NULL,           -- 背景颜色
    [IsBold         ] BIT             NOT NULL DEFAULT 0, -- 是否加粗
    CONSTRAINT [PK_ProjectStatusStyles] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProjectStatusStyles_ProjectStatuses_StatusId]
        FOREIGN KEY ([StatusId]) REFERENCES [ProjectStatuses] ([Id]),
    CONSTRAINT [UQ_ProjectStatusStyles_StatusId] UNIQUE ([StatusId])
);

-- --------------------------------------------------------------------------
-- 7. 请购记录表 (PurchaseRequests)
-- --------------------------------------------------------------------------
CREATE TABLE [PurchaseRequests] (
    [Id                   ] INT             IDENTITY(1,1) NOT NULL,
    [ProjectId            ] INT             NOT NULL,           -- 所属项目 ID
    [RequestNumber        ] NVARCHAR(64)    NOT NULL,           -- 请购号
    [PurchaseType         ] INT             NOT NULL,           -- 请购类型(1内购/2外购)
    [PurchaseStaffUserId  ] NVARCHAR(450)   NULL,               -- 请购人员
    [PurchaseAmount       ] DECIMAL(18,2)   NOT NULL DEFAULT 0, -- 请购金额
    [SubCaseContactUserId ] NVARCHAR(450)   NULL,               -- 子案对接人员
    [PaymentPercent       ] DECIMAL(5,2)    NOT NULL DEFAULT 0, -- 付款比例百分比
    [ActualPaidAmount     ] DECIMAL(18,2)   NOT NULL DEFAULT 0, -- 实际已付款
    [Notes                ] NVARCHAR(MAX)   NULL,               -- 备注
    [CreatedAt            ] DATETIMEOFFSET  NOT NULL,           -- 创建时间
    [UpdatedAt            ] DATETIMEOFFSET  NOT NULL,           -- 最近更新时间
    [IsDeleted            ] BIT             NOT NULL DEFAULT 0, -- 软删除标记
    CONSTRAINT [PK_PurchaseRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PurchaseRequests_Projects_ProjectId]
        FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]),
    CONSTRAINT [FK_PurchaseRequests_AspNetUsers_PurchaseStaffUserId]
        FOREIGN KEY ([PurchaseStaffUserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_PurchaseRequests_AspNetUsers_SubCaseContactUserId]
        FOREIGN KEY ([SubCaseContactUserId]) REFERENCES [AspNetUsers] ([Id])
);

-- --------------------------------------------------------------------------
-- 8. 月结批次表 (MonthlySettlementBatches)
-- --------------------------------------------------------------------------
CREATE TABLE [MonthlySettlementBatches] (
    [Id              ] INT             IDENTITY(1,1) NOT NULL,
    [Year            ] INT             NOT NULL,           -- 月结年度
    [Month           ] INT             NOT NULL,           -- 月结月份
    [BatchNumber     ] INT             NOT NULL,           -- 同年同月批次号
    [CreatedByUserId ] NVARCHAR(450)   NOT NULL,           -- 创建人
    [CreatedAt       ] DATETIMEOFFSET  NOT NULL,           -- 创建时间
    [Notes           ] NVARCHAR(MAX)   NULL,               -- 备注
    CONSTRAINT [PK_MonthlySettlementBatches] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MonthlySettlementBatches_AspNetUsers_CreatedByUserId]
        FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [UQ_MonthlySettlementBatches_Year_Month_BatchNumber]
        UNIQUE ([Year], [Month], [BatchNumber])
);

-- --------------------------------------------------------------------------
-- 9. 月结明细快照表 (MonthlySettlementItems)
-- --------------------------------------------------------------------------
CREATE TABLE [MonthlySettlementItems] (
    [Id                       ] INT             IDENTITY(1,1) NOT NULL,
    [BatchId                  ] INT             NOT NULL,           -- 所属批次
    [ProjectId                ] INT             NOT NULL,           -- 来源项目 ID
    [ParentCaseNumber         ] NVARCHAR(64)    NULL,               -- 母案案号快照
    [ProjectNumber            ] NVARCHAR(64)    NOT NULL,           -- 项目工号快照
    [ProjectName              ] NVARCHAR(200)   NOT NULL,           -- 项目名称快照
    [ProjectPersonnelText     ] NVARCHAR(MAX)   NOT NULL,           -- 项目人员汇总快照
    [ProgressPercent          ] DECIMAL(5,2)    NOT NULL DEFAULT 0, -- 进度快照
    [ProjectAmount            ] DECIMAL(18,2)   NOT NULL DEFAULT 0, -- 金额快照
    [CollectionPercent        ] DECIMAL(5,2)    NOT NULL DEFAULT 0, -- 收款比例快照
    [StatusName               ] NVARCHAR(MAX)   NOT NULL,           -- 状态名称快照
    [IsClosed                 ] BIT             NOT NULL DEFAULT 0, -- 是否结案快照
    [ClosedYearMonth          ] DATE            NULL,               -- 结案年月快照
    [PurchaseRequestSummary   ] NVARCHAR(MAX)   NOT NULL,           -- 请购号汇总快照
    [PurchaseAmountTotal      ] DECIMAL(18,2)   NOT NULL DEFAULT 0, -- 请购金额合计快照
    [SubCaseContactSummary    ] NVARCHAR(MAX)   NOT NULL,           -- 子案对接人员汇总快照
    [PaymentPercentSummary    ] NVARCHAR(MAX)   NOT NULL,           -- 付款比例汇总快照
    [ActualPaidAmountTotal    ] DECIMAL(18,2)   NOT NULL DEFAULT 0, -- 实际已付款合计快照
    [ProgressDescription      ] NVARCHAR(MAX)   NULL,               -- 进度说明快照
    [UpdatedByUserName        ] NVARCHAR(MAX)   NOT NULL,           -- 更新人名称快照
    [SourceUpdatedAt          ] DATETIMEOFFSET  NOT NULL,           -- 来源更新时间快照
    CONSTRAINT [PK_MonthlySettlementItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MonthlySettlementItems_MonthlySettlementBatches_BatchId]
        FOREIGN KEY ([BatchId]) REFERENCES [MonthlySettlementBatches] ([Id])
);

-- --------------------------------------------------------------------------
-- 10. 审计日志表 (AuditLogs)
-- --------------------------------------------------------------------------
CREATE TABLE [AuditLogs] (
    [Id                 ] INT             IDENTITY(1,1) NOT NULL,
    [UserId             ] NVARCHAR(450)   NULL,               -- 操作人
    [Action             ] NVARCHAR(80)    NOT NULL,           -- 操作类型
    [EntityName         ] NVARCHAR(120)   NOT NULL,           -- 实体名称
    [EntityId           ] NVARCHAR(120)   NOT NULL,           -- 实体 ID
    [Description        ] NVARCHAR(MAX)   NOT NULL,           -- 通用描述
    [ProjectId          ] INT             NULL,               -- 项目 ID
    [ProjectNumber      ] NVARCHAR(64)    NULL,               -- 项目工号快照
    [ChangeSummary      ] NVARCHAR(500)   NULL,               -- 变更摘要
    [ChangeDetailsJson  ] NVARCHAR(MAX)   NULL,               -- 字段级变更 JSON
    [CreatedAt          ] DATETIMEOFFSET  NOT NULL,           -- 操作时间
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AuditLogs_AspNetUsers_UserId]
        FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id])
);
CREATE INDEX [IX_AuditLogs_ProjectId] ON [AuditLogs] ([ProjectId]);

-- --------------------------------------------------------------------------
-- 11. 规划中项目表 (PlanningProjects)
-- --------------------------------------------------------------------------
CREATE TABLE [PlanningProjects] (
    [Id                 ] INT             IDENTITY(1,1) NOT NULL,
    [Name               ] NVARCHAR(200)   NOT NULL,           -- 项目名
    [LeaderUserId       ] NVARCHAR(450)   NULL,               -- 项目负责人
    [LatestDescription  ] NVARCHAR(MAX)   NULL,               -- 最新说明
    [CreatedAt          ] DATETIMEOFFSET  NOT NULL,           -- 创建时间
    [UpdatedAt          ] DATETIMEOFFSET  NOT NULL,           -- 最近更新时间
    [IsDeleted          ] BIT             NOT NULL DEFAULT 0, -- 软删除标记
    CONSTRAINT [PK_PlanningProjects] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PlanningProjects_AspNetUsers_LeaderUserId]
        FOREIGN KEY ([LeaderUserId]) REFERENCES [AspNetUsers] ([Id])
);

-- --------------------------------------------------------------------------
-- 12. 规划中项目月度历史记录表 (PlanningProjectHistoryRecords)
-- --------------------------------------------------------------------------
CREATE TABLE [PlanningProjectHistoryRecords] (
    [Id                  ] INT             IDENTITY(1,1) NOT NULL,
    [PlanningProjectId   ] INT             NOT NULL,           -- 所属规划项目
    [Year                ] INT             NOT NULL,           -- 记录年份
    [Month               ] INT             NOT NULL,           -- 记录月份
    [PreviousDescription ] NVARCHAR(MAX)   NULL,               -- 上期说明
    [CurrentRecord       ] NVARCHAR(MAX)   NULL,               -- 本期记录
    [CreatedByUserId     ] NVARCHAR(450)   NULL,               -- 记录人
    [CreatedAt           ] DATETIMEOFFSET  NOT NULL,           -- 记录时间
    CONSTRAINT [PK_PlanningProjectHistoryRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PlanningProjectHistoryRecords_PlanningProjects_PlanningProjectId]
        FOREIGN KEY ([PlanningProjectId]) REFERENCES [PlanningProjects] ([Id]),
    CONSTRAINT [FK_PlanningProjectHistoryRecords_AspNetUsers_CreatedByUserId]
        FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id])
);
CREATE INDEX [IX_PlanningProjectHistoryRecords_PlanningProjectId_Year_Month]
    ON [PlanningProjectHistoryRecords] ([PlanningProjectId], [Year], [Month]);

-- --------------------------------------------------------------------------
-- 13. 保养订单表 (MaintenanceOrders)
-- --------------------------------------------------------------------------
CREATE TABLE [MaintenanceOrders] (
    [Id                    ] INT             IDENTITY(1,1) NOT NULL,
    [Year                  ] INT             NOT NULL,           -- 年度
    [CustomerName          ] NVARCHAR(200)   NOT NULL,           -- 客户名称
    [MaintenanceStartDate  ] DATE            NOT NULL,           -- 保养开始日期
    [MaintenanceEndDate    ] DATE            NOT NULL,           -- 保养结束日期
    [MaintenanceMethod     ] INT             NOT NULL,           -- 保养方式(1现场/2远程/3均有)
    [OnSiteAnnualCount     ] INT             NOT NULL DEFAULT 0, -- 现场年度次数
    [RemoteAnnualCount     ] INT             NOT NULL DEFAULT 0, -- 远程年度次数
    [ExecutorUserId        ] NVARCHAR(450)   NULL,               -- 保养执行人
    [HandoverPercent       ] DECIMAL(5,2)    NOT NULL DEFAULT 0, -- 签收单移交百分比
    [UpdatedByUserId       ] NVARCHAR(450)   NULL,               -- 更新人员
    [UpdatedAt             ] DATETIMEOFFSET  NOT NULL,           -- 更新日期
    [CreatedAt             ] DATETIMEOFFSET  NOT NULL,           -- 创建时间
    [IsDeleted             ] BIT             NOT NULL DEFAULT 0, -- 软删除标记
    CONSTRAINT [PK_MaintenanceOrders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceOrders_AspNetUsers_ExecutorUserId]
        FOREIGN KEY ([ExecutorUserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_MaintenanceOrders_AspNetUsers_UpdatedByUserId]
        FOREIGN KEY ([UpdatedByUserId]) REFERENCES [AspNetUsers] ([Id])
);

-- ==========================================================================
-- 表结构字典说明
-- ==========================================================================
-- 表名                          说明
-- ---------------------------- ------------------------------------------------
-- AspNetUsers                  系统用户（Identity + 扩展字段）
-- AspNetRoles                  角色字典
-- AspNetUserRoles              用户角色关系
-- Projects                     项目主数据
-- ProjectAssignments           项目人员分配关系
-- ProjectStatuses              项目状态字典
-- ProjectStatusStyles          项目状态显示样式
-- PurchaseRequests             请购记录
-- MonthlySettlementBatches     月结批次头
-- MonthlySettlementItems       月结明细快照
-- AuditLogs                    审计日志
-- PlanningProjects             规划中项目
-- PlanningProjectHistoryRecords 规划中项目月度历史记录
-- MaintenanceOrders            保养订单
-- ==========================================================================
