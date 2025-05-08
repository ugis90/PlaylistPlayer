using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetManager.Migrations
{
    /// <inheritdoc />
    public partial class RefactorMaintenanceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRecords_Trips_TripId",
                table: "MaintenanceRecords");

            migrationBuilder.RenameColumn(
                name: "TripId",
                table: "MaintenanceRecords",
                newName: "VehicleId");

            migrationBuilder.RenameIndex(
                name: "IX_MaintenanceRecords_TripId",
                table: "MaintenanceRecords",
                newName: "IX_MaintenanceRecords_VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRecords_Vehicles_VehicleId",
                table: "MaintenanceRecords",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRecords_Vehicles_VehicleId",
                table: "MaintenanceRecords");

            migrationBuilder.RenameColumn(
                name: "VehicleId",
                table: "MaintenanceRecords",
                newName: "TripId");

            migrationBuilder.RenameIndex(
                name: "IX_MaintenanceRecords_VehicleId",
                table: "MaintenanceRecords",
                newName: "IX_MaintenanceRecords_TripId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRecords_Trips_TripId",
                table: "MaintenanceRecords",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
