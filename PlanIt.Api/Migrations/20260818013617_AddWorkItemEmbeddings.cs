using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace PlanIt.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "WorkItemEmbeddingsOnnx",
                columns: table => new
                {
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Vector = table.Column<Vector>(type: "vector(384)", nullable: false),
                    SourceText = table.Column<string>(type: "text", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemEmbeddingsOnnx", x => x.WorkItemId);
                    table.ForeignKey(
                        name: "FK_WorkItemEmbeddingsOnnx_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemEmbeddingsPython",
                columns: table => new
                {
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Vector = table.Column<Vector>(type: "vector(768)", nullable: false),
                    SourceText = table.Column<string>(type: "text", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemEmbeddingsPython", x => x.WorkItemId);
                    table.ForeignKey(
                        name: "FK_WorkItemEmbeddingsPython_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemEmbeddingsOnnx");

            migrationBuilder.DropTable(
                name: "WorkItemEmbeddingsPython");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
