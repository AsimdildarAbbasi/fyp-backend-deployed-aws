using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OBManagementAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddGeofenceAndSubstituteSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GeofenceId",
                table: "Task",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaskCategoryId",
                table: "Task",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerType",
                table: "Task",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TriggeredAt",
                table: "Task",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubstituteOfficeBoyAccountId",
                table: "LeaveRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OriginalAssignmentId",
                table: "LeaveRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubstituteAssignmentId",
                table: "LeaveRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedAt",
                table: "LeaveRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Task_GeofenceId",
                table: "Task",
                column: "GeofenceId");

            migrationBuilder.CreateIndex(
                name: "IX_Task_TaskCategoryId",
                table: "Task",
                column: "TaskCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_SubstituteOfficeBoyAccountId",
                table: "LeaveRequests",
                column: "SubstituteOfficeBoyAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Task_Geofence",
                table: "Task",
                column: "GeofenceId",
                principalTable: "Geofence",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Task_TaskCategory",
                table: "Task",
                column: "TaskCategoryId",
                principalTable: "TaskCategory",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequests_Account_SubstituteOfficeBoyAccountId",
                table: "LeaveRequests",
                column: "SubstituteOfficeBoyAccountId",
                principalTable: "Account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Task_Geofence",
                table: "Task");

            migrationBuilder.DropForeignKey(
                name: "FK_Task_TaskCategory",
                table: "Task");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequests_Account_SubstituteOfficeBoyAccountId",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_Task_GeofenceId",
                table: "Task");

            migrationBuilder.DropIndex(
                name: "IX_Task_TaskCategoryId",
                table: "Task");

            migrationBuilder.DropColumn(
                name: "GeofenceId",
                table: "Task");

            migrationBuilder.DropColumn(
                name: "TaskCategoryId",
                table: "Task");

            migrationBuilder.DropColumn(
                name: "TriggerType",
                table: "Task");

            migrationBuilder.DropColumn(
                name: "TriggeredAt",
                table: "Task");

            migrationBuilder.DropColumn(
                name: "IX_LeaveRequests_SubstituteOfficeBoyAccountId",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "SubstituteOfficeBoyAccountId",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "OriginalAssignmentId",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "SubstituteAssignmentId",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "ReturnedAt",
                table: "LeaveRequests");
        }
    }
}
