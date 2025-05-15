using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetManager.Migrations
{
    /// <inheritdoc />
    public partial class EuropeanUnitsAndYoungDriverRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Gallons",
                table: "FuelRecords",
                newName: "Liters");

            migrationBuilder.RenameColumn(
                name: "CostPerGallon",
                table: "FuelRecords",
                newName: "CostPerLiter");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Liters",
                table: "FuelRecords",
                newName: "Gallons");

            migrationBuilder.RenameColumn(
                name: "CostPerLiter",
                table: "FuelRecords",
                newName: "CostPerGallon");
        }
    }
}
