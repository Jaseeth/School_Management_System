using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurableMondayAssembly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "MondayAssemblyEnd",
                table: "SchoolProfiles",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(8, 15, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "MondayAssemblyStart",
                table: "SchoolProfiles",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(7, 30, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MondayAssemblyEnd",
                table: "SchoolProfiles");

            migrationBuilder.DropColumn(
                name: "MondayAssemblyStart",
                table: "SchoolProfiles");
        }
    }
}
