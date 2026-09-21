using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentGraduationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GraduationAcademicYearId",
                table: "Students",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "GraduationDate",
                table: "Students",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGraduated",
                table: "Students",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Students_GraduationAcademicYearId",
                table: "Students",
                column: "GraduationAcademicYearId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_AcademicYears_GraduationAcademicYearId",
                table: "Students",
                column: "GraduationAcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_AcademicYears_GraduationAcademicYearId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_GraduationAcademicYearId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "GraduationAcademicYearId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "GraduationDate",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "IsGraduated",
                table: "Students");
        }
    }
}
