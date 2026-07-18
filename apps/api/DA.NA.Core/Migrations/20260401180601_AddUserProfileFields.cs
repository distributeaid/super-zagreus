using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DA.NA.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Organisations_Organisations_HubOfOrgId",
                table: "Organisations");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Organisations_OrgId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Organisations_HubOfOrgId",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "HubOfOrgId",
                table: "Organisations");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrgId",
                table: "Users",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "OrgAssociations",
                columns: table => new
                {
                    ParentOrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildOrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgAssociations", x => new { x.ParentOrgId, x.ChildOrgId });
                    table.ForeignKey(
                        name: "FK_OrgAssociations_Organisations_ChildOrgId",
                        column: x => x.ChildOrgId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrgAssociations_Organisations_ParentOrgId",
                        column: x => x.ParentOrgId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgAssociations_ChildOrgId",
                table: "OrgAssociations",
                column: "ChildOrgId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Organisations_OrgId",
                table: "Users",
                column: "OrgId",
                principalTable: "Organisations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Organisations_OrgId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "OrgAssociations");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Users");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrgId",
                table: "Users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HubOfOrgId",
                table: "Organisations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organisations_HubOfOrgId",
                table: "Organisations",
                column: "HubOfOrgId");

            migrationBuilder.AddForeignKey(
                name: "FK_Organisations_Organisations_HubOfOrgId",
                table: "Organisations",
                column: "HubOfOrgId",
                principalTable: "Organisations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Organisations_OrgId",
                table: "Users",
                column: "OrgId",
                principalTable: "Organisations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
