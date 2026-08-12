using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanIt.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Order",
                table: "WorkItems",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Order",
                table: "WorkItems");
        }
    }
}
