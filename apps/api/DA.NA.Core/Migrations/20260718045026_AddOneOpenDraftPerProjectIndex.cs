using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DA.NA.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddOneOpenDraftPerProjectIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NeedsAssessments_ProjectId",
                table: "NeedsAssessments");

            migrationBuilder.CreateIndex(
                name: "IX_NeedsAssessments_ProjectId",
                table: "NeedsAssessments",
                column: "ProjectId",
                unique: true,
                filter: "\"Status\" = 'Draft'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NeedsAssessments_ProjectId",
                table: "NeedsAssessments");

            migrationBuilder.CreateIndex(
                name: "IX_NeedsAssessments_ProjectId",
                table: "NeedsAssessments",
                column: "ProjectId");
        }
    }
}
