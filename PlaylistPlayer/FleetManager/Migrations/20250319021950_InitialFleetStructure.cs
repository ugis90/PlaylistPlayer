using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FleetManager.Migrations
{
    /// <inheritdoc />
    public partial class InitialFleetStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_AspNetUsers_UserId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Playlists_AspNetUsers_UserId",
                table: "Playlists");

            migrationBuilder.DropForeignKey(
                name: "FK_Playlists_Categories_CategoryId",
                table: "Playlists");

            migrationBuilder.DropForeignKey(
                name: "FK_Songs_AspNetUsers_UserId",
                table: "Songs");

            migrationBuilder.DropForeignKey(
                name: "FK_Songs_Playlists_PlaylistId",
                table: "Songs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Songs",
                table: "Songs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Playlists",
                table: "Playlists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Categories",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Songs");

            migrationBuilder.RenameTable(
                name: "Songs",
                newName: "MaintenanceRecords");

            migrationBuilder.RenameTable(
                name: "Playlists",
                newName: "Trips");

            migrationBuilder.RenameTable(
                name: "Categories",
                newName: "Vehicles");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "MaintenanceRecords",
                newName: "ServiceType");

            migrationBuilder.RenameColumn(
                name: "PlaylistId",
                table: "MaintenanceRecords",
                newName: "TripId");

            migrationBuilder.RenameColumn(
                name: "OrderId",
                table: "MaintenanceRecords",
                newName: "Mileage");

            migrationBuilder.RenameColumn(
                name: "Artist",
                table: "MaintenanceRecords",
                newName: "Provider");

            migrationBuilder.RenameIndex(
                name: "IX_Songs_UserId",
                table: "MaintenanceRecords",
                newName: "IX_MaintenanceRecords_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Songs_PlaylistId",
                table: "MaintenanceRecords",
                newName: "IX_MaintenanceRecords_TripId");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Trips",
                newName: "StartLocation");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Trips",
                newName: "Purpose");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "Trips",
                newName: "VehicleId");

            migrationBuilder.RenameIndex(
                name: "IX_Playlists_UserId",
                table: "Trips",
                newName: "IX_Trips_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Playlists_CategoryId",
                table: "Trips",
                newName: "IX_Trips_VehicleId");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Vehicles",
                newName: "Model");

            migrationBuilder.RenameIndex(
                name: "IX_Categories_UserId",
                table: "Vehicles",
                newName: "IX_Vehicles_UserId");

            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "MaintenanceRecords",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Date",
                table: "MaintenanceRecords",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "MaintenanceRecords",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextServiceDue",
                table: "MaintenanceRecords",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Distance",
                table: "Trips",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "EndLocation",
                table: "Trips",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndTime",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<double>(
                name: "FuelUsed",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartTime",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "CurrentMileage",
                table: "Vehicles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LicensePlate",
                table: "Vehicles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Make",
                table: "Vehicles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "Vehicles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_MaintenanceRecords",
                table: "MaintenanceRecords",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Trips",
                table: "Trips",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Vehicles",
                table: "Vehicles",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "FuelRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Gallons = table.Column<double>(type: "double precision", nullable: false),
                    CostPerGallon = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric", nullable: false),
                    Mileage = table.Column<int>(type: "integer", nullable: false),
                    Station = table.Column<string>(type: "text", nullable: false),
                    FullTank = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuelRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FuelRecords_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FuelRecords_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FuelRecords_UserId",
                table: "FuelRecords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FuelRecords_VehicleId",
                table: "FuelRecords",
                column: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRecords_AspNetUsers_UserId",
                table: "MaintenanceRecords",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRecords_Trips_TripId",
                table: "MaintenanceRecords",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_AspNetUsers_UserId",
                table: "Trips",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_Vehicles_VehicleId",
                table: "Trips",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_AspNetUsers_UserId",
                table: "Vehicles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRecords_AspNetUsers_UserId",
                table: "MaintenanceRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRecords_Trips_TripId",
                table: "MaintenanceRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Trips_AspNetUsers_UserId",
                table: "Trips");

            migrationBuilder.DropForeignKey(
                name: "FK_Trips_Vehicles_VehicleId",
                table: "Trips");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_AspNetUsers_UserId",
                table: "Vehicles");

            migrationBuilder.DropTable(
                name: "FuelRecords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Vehicles",
                table: "Vehicles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Trips",
                table: "Trips");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MaintenanceRecords",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "CurrentMileage",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "LicensePlate",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Make",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Distance",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "EndLocation",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "FuelUsed",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "NextServiceDue",
                table: "MaintenanceRecords");

            migrationBuilder.RenameTable(
                name: "Vehicles",
                newName: "Categories");

            migrationBuilder.RenameTable(
                name: "Trips",
                newName: "Playlists");

            migrationBuilder.RenameTable(
                name: "MaintenanceRecords",
                newName: "Songs");

            migrationBuilder.RenameColumn(
                name: "Model",
                table: "Categories",
                newName: "Name");

            migrationBuilder.RenameIndex(
                name: "IX_Vehicles_UserId",
                table: "Categories",
                newName: "IX_Categories_UserId");

            migrationBuilder.RenameColumn(
                name: "VehicleId",
                table: "Playlists",
                newName: "CategoryId");

            migrationBuilder.RenameColumn(
                name: "StartLocation",
                table: "Playlists",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "Purpose",
                table: "Playlists",
                newName: "Description");

            migrationBuilder.RenameIndex(
                name: "IX_Trips_VehicleId",
                table: "Playlists",
                newName: "IX_Playlists_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Trips_UserId",
                table: "Playlists",
                newName: "IX_Playlists_UserId");

            migrationBuilder.RenameColumn(
                name: "TripId",
                table: "Songs",
                newName: "PlaylistId");

            migrationBuilder.RenameColumn(
                name: "ServiceType",
                table: "Songs",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "Provider",
                table: "Songs",
                newName: "Artist");

            migrationBuilder.RenameColumn(
                name: "Mileage",
                table: "Songs",
                newName: "OrderId");

            migrationBuilder.RenameIndex(
                name: "IX_MaintenanceRecords_UserId",
                table: "Songs",
                newName: "IX_Songs_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_MaintenanceRecords_TripId",
                table: "Songs",
                newName: "IX_Songs_PlaylistId");

            migrationBuilder.AddColumn<int>(
                name: "Duration",
                table: "Songs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Categories",
                table: "Categories",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Playlists",
                table: "Playlists",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Songs",
                table: "Songs",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_AspNetUsers_UserId",
                table: "Categories",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Playlists_AspNetUsers_UserId",
                table: "Playlists",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Playlists_Categories_CategoryId",
                table: "Playlists",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Songs_AspNetUsers_UserId",
                table: "Songs",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Songs_Playlists_PlaylistId",
                table: "Songs",
                column: "PlaylistId",
                principalTable: "Playlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
