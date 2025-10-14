using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeIndex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssistantId",
                table: "Projects",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssistantId",
                table: "Projects");
        }
    }
}
