using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaylistPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderIdToSong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrderId",
                table: "Songs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "Songs");
        }
    }
}
