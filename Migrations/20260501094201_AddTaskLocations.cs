using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OBManagementAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Account",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Password = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Account__3214EC07BB7B523E", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BuildingFloor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Building__3214EC070E414C38", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Location",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Location__3214EC076A76558D", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FacultyTracking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacultyAccountId = table.Column<int>(type: "int", nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    Time = table.Column<TimeOnly>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__FacultyT__3214EC079197F328", x => x.Id);
                    table.ForeignKey(
                        name: "FK__FacultyTr__Facul__619B8048",
                        column: x => x.FacultyAccountId,
                        principalTable: "Account",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Office",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OfficeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BuildingFloorId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Office__3214EC07AE5D7385", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Office__Building__4E88ABD4",
                        column: x => x.BuildingFloorId,
                        principalTable: "BuildingFloor",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Task",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacultyAccountId = table.Column<int>(type: "int", nullable: false),
                    OfficeBoyAccountId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TaskTime = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsScheduled = table.Column<bool>(type: "bit", nullable: false),
                    CurrentLocationId = table.Column<int>(type: "int", nullable: true),
                    LocationUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Task__3214EC07C2FACE9A", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Task__CurrentLocation",
                        column: x => x.CurrentLocationId,
                        principalTable: "Location",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Task__FacultyAcc__5CD6CB2B",
                        column: x => x.FacultyAccountId,
                        principalTable: "Account",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Task__LocationId__5EBF139D",
                        column: x => x.LocationId,
                        principalTable: "Location",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Task__OfficeBoyA__5DCAEF64",
                        column: x => x.OfficeBoyAccountId,
                        principalTable: "Account",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FacultyMemberOffice",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacultyAccountId = table.Column<int>(type: "int", nullable: false),
                    OfficeId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__FacultyM__3214EC07CEFD6BEC", x => x.Id);
                    table.ForeignKey(
                        name: "FK__FacultyMe__Facul__5165187F",
                        column: x => x.FacultyAccountId,
                        principalTable: "Account",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__FacultyMe__Offic__52593CB8",
                        column: x => x.OfficeId,
                        principalTable: "Office",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OfficeBoyAssignedFloors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FloorId = table.Column<int>(type: "int", nullable: false),
                    OfficeId = table.Column<int>(type: "int", nullable: false),
                    OfficeBoyAccountId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__OfficeBo__3214EC072AB34896", x => x.Id);
                    table.ForeignKey(
                        name: "FK__OfficeBoy__Floor__5535A963",
                        column: x => x.FloorId,
                        principalTable: "BuildingFloor",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__OfficeBoy__Offic__5629CD9C",
                        column: x => x.OfficeId,
                        principalTable: "Office",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__OfficeBoy__Offic__571DF1D5",
                        column: x => x.OfficeBoyAccountId,
                        principalTable: "Account",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FacultyMemberOffice_FacultyAccountId",
                table: "FacultyMemberOffice",
                column: "FacultyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FacultyMemberOffice_OfficeId",
                table: "FacultyMemberOffice",
                column: "OfficeId");

            migrationBuilder.CreateIndex(
                name: "IX_FacultyTracking_FacultyAccountId",
                table: "FacultyTracking",
                column: "FacultyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Office_BuildingFloorId",
                table: "Office",
                column: "BuildingFloorId");

            migrationBuilder.CreateIndex(
                name: "IX_OfficeBoyAssignedFloors_FloorId",
                table: "OfficeBoyAssignedFloors",
                column: "FloorId");

            migrationBuilder.CreateIndex(
                name: "IX_OfficeBoyAssignedFloors_OfficeBoyAccountId",
                table: "OfficeBoyAssignedFloors",
                column: "OfficeBoyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_OfficeBoyAssignedFloors_OfficeId",
                table: "OfficeBoyAssignedFloors",
                column: "OfficeId");

            migrationBuilder.CreateIndex(
                name: "IX_Task_CurrentLocationId",
                table: "Task",
                column: "CurrentLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Task_FacultyAccountId",
                table: "Task",
                column: "FacultyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Task_LocationId",
                table: "Task",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Task_OfficeBoyAccountId",
                table: "Task",
                column: "OfficeBoyAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacultyMemberOffice");

            migrationBuilder.DropTable(
                name: "FacultyTracking");

            migrationBuilder.DropTable(
                name: "OfficeBoyAssignedFloors");

            migrationBuilder.DropTable(
                name: "Task");

            migrationBuilder.DropTable(
                name: "Office");

            migrationBuilder.DropTable(
                name: "Location");

            migrationBuilder.DropTable(
                name: "Account");

            migrationBuilder.DropTable(
                name: "BuildingFloor");
        }
    }
}
