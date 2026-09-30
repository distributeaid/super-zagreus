using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DA.NA.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddFreshnessEmailSentFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PreStaleEmailSentAt",
                table: "NeedsAssessments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StaleEmailSentAt",
                table: "NeedsAssessments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreStaleEmailSentAt",
                table: "NeedsAssessments");

            migrationBuilder.DropColumn(
                name: "StaleEmailSentAt",
                table: "NeedsAssessments");
        }
    }
}
