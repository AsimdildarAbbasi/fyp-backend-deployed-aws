using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OBManagementAPI.Migrations
{
    /// <inheritdoc />
    public partial class FeatureTaskLeaveNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The GeofenceTaskDetail entity previously existed in the model without a
            // migration. Some databases already have the table (created manually), while
            // a fresh database does not. Create it only when it is missing.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[GeofenceTaskDetail]', N'U') IS NULL
BEGIN
    CREATE TABLE [GeofenceTaskDetail] (
        [TaskId] int NOT NULL,
        [FacultyAccountId] int NOT NULL,
        [GeofenceId] int NOT NULL,
        [TriggerType] varchar(10) NOT NULL,
        [TaskCategoryId] int NULL,
        [IsVisibleToOfficeBoy] bit NOT NULL,
        [TriggeredAt] datetime2 NULL,
        CONSTRAINT [PK_GeofenceTaskDetail] PRIMARY KEY ([TaskId]),
        CONSTRAINT [FK_GeofenceTaskDetail_Account] FOREIGN KEY ([FacultyAccountId]) REFERENCES [Account] ([Id]),
        CONSTRAINT [FK_GeofenceTaskDetail_Geofence] FOREIGN KEY ([GeofenceId]) REFERENCES [Geofence] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_GeofenceTaskDetail_Task] FOREIGN KEY ([TaskId]) REFERENCES [Task] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_GeofenceTaskDetail_TaskCategory] FOREIGN KEY ([TaskCategoryId]) REFERENCES [TaskCategory] ([Id])
    );
END
");

            // Remove the old geofence columns that were stored directly on Task.
            // These drops are guarded so the migration also works on databases where
            // one or more of the legacy columns were never added.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[FK_Task_Geofence]', N'F') IS NOT NULL
    ALTER TABLE [Task] DROP CONSTRAINT [FK_Task_Geofence];

IF OBJECT_ID(N'[FK_Task_TaskCategory]', N'F') IS NOT NULL
    ALTER TABLE [Task] DROP CONSTRAINT [FK_Task_TaskCategory];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Task_GeofenceId' AND object_id = OBJECT_ID(N'[Task]'))
    DROP INDEX [IX_Task_GeofenceId] ON [Task];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Task_TaskCategoryId' AND object_id = OBJECT_ID(N'[Task]'))
    DROP INDEX [IX_Task_TaskCategoryId] ON [Task];

DECLARE @legacyDefaults nvarchar(max) = N'';
SELECT @legacyDefaults = @legacyDefaults + N'ALTER TABLE [Task] DROP CONSTRAINT [' + dc.name + N'];'
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID(N'[Task]')
  AND c.name IN (
      N'IsVisibleToOfficeBoy',
      N'TriggerType',
      N'TriggeredAt',
      N'GeofenceId',
      N'TaskCategoryId');

IF LEN(@legacyDefaults) > 0
    EXEC sp_executesql @legacyDefaults;

IF COL_LENGTH(N'[Task]', N'IsVisibleToOfficeBoy') IS NOT NULL
    ALTER TABLE [Task] DROP COLUMN [IsVisibleToOfficeBoy];

IF COL_LENGTH(N'[Task]', N'TriggerType') IS NOT NULL
    ALTER TABLE [Task] DROP COLUMN [TriggerType];

IF COL_LENGTH(N'[Task]', N'TriggeredAt') IS NOT NULL
    ALTER TABLE [Task] DROP COLUMN [TriggeredAt];

IF COL_LENGTH(N'[Task]', N'GeofenceId') IS NOT NULL
    ALTER TABLE [Task] DROP COLUMN [GeofenceId];

IF COL_LENGTH(N'[Task]', N'TaskCategoryId') IS NOT NULL
    ALTER TABLE [Task] DROP COLUMN [TaskCategoryId];
");

            // New Task columns for editing and substitute tracking.
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Task]', N'IsOutsideCampus') IS NULL
    ALTER TABLE [Task] ADD [IsOutsideCampus] bit NOT NULL CONSTRAINT [DF_Task_IsOutsideCampus] DEFAULT (0);

IF COL_LENGTH(N'[Task]', N'UpdatedAt') IS NULL
    ALTER TABLE [Task] ADD [UpdatedAt] datetime NULL;

IF COL_LENGTH(N'[Task]', N'UpdatedByAccountId') IS NULL
    ALTER TABLE [Task] ADD [UpdatedByAccountId] int NULL;

IF COL_LENGTH(N'[Task]', N'OriginalOfficeBoyAccountId') IS NULL
    ALTER TABLE [Task] ADD [OriginalOfficeBoyAccountId] int NULL;
");

            // Alignment indexes for the GeofenceTaskDetail table.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_GeofenceTaskDetail_FacultyAccountId' AND object_id = OBJECT_ID(N'[GeofenceTaskDetail]'))
    CREATE INDEX [IX_GeofenceTaskDetail_FacultyAccountId] ON [GeofenceTaskDetail] ([FacultyAccountId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_GeofenceTaskDetail_GeofenceId' AND object_id = OBJECT_ID(N'[GeofenceTaskDetail]'))
    CREATE INDEX [IX_GeofenceTaskDetail_GeofenceId] ON [GeofenceTaskDetail] ([GeofenceId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_GeofenceTaskDetail_TaskCategoryId' AND object_id = OBJECT_ID(N'[GeofenceTaskDetail]'))
    CREATE INDEX [IX_GeofenceTaskDetail_TaskCategoryId] ON [GeofenceTaskDetail] ([TaskCategoryId]);
");

            migrationBuilder.CreateTable(
                name: "LeavePolicy",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Role = table.Column<int>(type: "int", nullable: false),
                    YearlyLimit = table.Column<int>(type: "int", nullable: false),
                    MonthlyLimit = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeavePolicy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipientAccountId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TaskId = table.Column<int>(type: "int", nullable: true),
                    OfficeBoyAccountId = table.Column<int>(type: "int", nullable: true),
                    FloorId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notification_Floor",
                        column: x => x.FloorId,
                        principalTable: "BuildingFloor",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Notification_Location",
                        column: x => x.LocationId,
                        principalTable: "Location",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Notification_OfficeBoy",
                        column: x => x.OfficeBoyAccountId,
                        principalTable: "Account",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Notification_Recipient",
                        column: x => x.RecipientAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notification_Task",
                        column: x => x.TaskId,
                        principalTable: "Task",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TaskAttachment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoredPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAttachment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskAttachment_Task",
                        column: x => x.TaskId,
                        principalTable: "Task",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "LeavePolicy",
                columns: new[] { "Id", "EffectiveFrom", "IsActive", "MonthlyLimit", "Role", "YearlyLimit" },
                values: new object[] { 1, new DateTime(2020, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, 4, 1, 25 });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_FloorId",
                table: "Notification",
                column: "FloorId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_LocationId",
                table: "Notification",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_OfficeBoyAccountId",
                table: "Notification",
                column: "OfficeBoyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_RecipientAccountId",
                table: "Notification",
                column: "RecipientAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_TaskId",
                table: "Notification",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAttachment_TaskId",
                table: "TaskAttachment",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeavePolicy");

            migrationBuilder.DropTable(
                name: "Notification");

            migrationBuilder.DropTable(
                name: "TaskAttachment");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Task]', N'IsOutsideCampus') IS NOT NULL
    ALTER TABLE [Task] DROP CONSTRAINT [DF_Task_IsOutsideCampus];

IF COL_LENGTH(N'[Task]', N'IsOutsideCampus') IS NOT NULL
    ALTER TABLE [Task] DROP COLUMN [IsOutsideCampus];

IF COL_LENGTH(N'[Task]', N'UpdatedAt') IS NOT NULL
    ALTER TABLE [Task] DROP COLUMN [UpdatedAt];

IF COL_LENGTH(N'[Task]', N'UpdatedByAccountId') IS NOT NULL
    ALTER TABLE [Task] DROP COLUMN [UpdatedByAccountId];

IF COL_LENGTH(N'[Task]', N'OriginalOfficeBoyAccountId') IS NOT NULL
    ALTER TABLE [Task] DROP COLUMN [OriginalOfficeBoyAccountId];

IF COL_LENGTH(N'[Task]', N'IsVisibleToOfficeBoy') IS NULL
    ALTER TABLE [Task] ADD [IsVisibleToOfficeBoy] bit NOT NULL DEFAULT (0);

IF COL_LENGTH(N'[Task]', N'GeofenceId') IS NULL
    ALTER TABLE [Task] ADD [GeofenceId] int NULL;

IF COL_LENGTH(N'[Task]', N'TaskCategoryId') IS NULL
    ALTER TABLE [Task] ADD [TaskCategoryId] int NULL;

IF COL_LENGTH(N'[Task]', N'TriggerType') IS NULL
    ALTER TABLE [Task] ADD [TriggerType] varchar(50) NULL;

IF COL_LENGTH(N'[Task]', N'TriggeredAt') IS NULL
    ALTER TABLE [Task] ADD [TriggeredAt] datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Task_GeofenceId' AND object_id = OBJECT_ID(N'[Task]'))
    CREATE INDEX [IX_Task_GeofenceId] ON [Task] ([GeofenceId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Task_TaskCategoryId' AND object_id = OBJECT_ID(N'[Task]'))
    CREATE INDEX [IX_Task_TaskCategoryId] ON [Task] ([TaskCategoryId]);

IF OBJECT_ID(N'[FK_Task_Geofence]', N'F') IS NULL
    ALTER TABLE [Task] ADD CONSTRAINT [FK_Task_Geofence] FOREIGN KEY ([GeofenceId]) REFERENCES [Geofence] ([Id]);

IF OBJECT_ID(N'[FK_Task_TaskCategory]', N'F') IS NULL
    ALTER TABLE [Task] ADD CONSTRAINT [FK_Task_TaskCategory] FOREIGN KEY ([TaskCategoryId]) REFERENCES [TaskCategory] ([Id]);
");
        }
    }
}
