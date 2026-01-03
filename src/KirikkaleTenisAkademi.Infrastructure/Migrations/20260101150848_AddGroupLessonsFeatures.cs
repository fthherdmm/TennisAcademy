using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KirikkaleTenisAkademi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupLessonsFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupCredits",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "GroupLessons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CoachId = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    RegisteredCount = table.Column<int>(type: "integer", nullable: false),
                    MinLevel = table.Column<int>(type: "integer", nullable: false),
                    CreditCost = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupLessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupLessons_Coaches_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupLessonRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupLessonId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<string>(type: "text", nullable: false),
                    RegistrationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsAttended = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupLessonRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupLessonRegistrations_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupLessonRegistrations_GroupLessons_GroupLessonId",
                        column: x => x.GroupLessonId,
                        principalTable: "GroupLessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupLessonRegistrations_GroupLessonId",
                table: "GroupLessonRegistrations",
                column: "GroupLessonId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupLessonRegistrations_StudentId",
                table: "GroupLessonRegistrations",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupLessons_CoachId",
                table: "GroupLessons",
                column: "CoachId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupLessonRegistrations");

            migrationBuilder.DropTable(
                name: "GroupLessons");

            migrationBuilder.DropColumn(
                name: "GroupCredits",
                table: "AspNetUsers");
        }
    }
}
