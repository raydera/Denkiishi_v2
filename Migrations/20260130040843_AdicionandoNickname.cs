using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Denkiishi_v2.Migrations
{
    /// <inheritdoc />
    public partial class AdicionandoNickname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "kanji",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "category",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kanji_audit_log",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    kanji_id = table.Column<int>(type: "integer", nullable: false),
                    action_type = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    changed_fields = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kanji_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "FK_kanji_audit_log_kanji_kanji_id",
                        column: x => x.kanji_id,
                        principalTable: "kanji",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kanji_category_map",
                columns: table => new
                {
                    kanji_id = table.Column<int>(type: "integer", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    category_level = table.Column<string>(type: "text", nullable: true),
                    incl_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kanji_category_map", x => new { x.kanji_id, x.category_id });
                    table.ForeignKey(
                        name: "FK_kanji_category_map_category_category_id",
                        column: x => x.category_id,
                        principalTable: "category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_kanji_category_map_kanji_kanji_id",
                        column: x => x.kanji_id,
                        principalTable: "kanji",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_kanji_audit_log_kanji_id",
                table: "kanji_audit_log",
                column: "kanji_id");

            migrationBuilder.CreateIndex(
                name: "IX_kanji_category_map_category_id",
                table: "kanji_category_map",
                column: "category_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kanji_audit_log");

            migrationBuilder.DropTable(
                name: "kanji_category_map");

            migrationBuilder.DropTable(
                name: "category");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "kanji");
        }
    }
}
