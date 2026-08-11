using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNavigations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserPlans_PlanId",
                table: "UserPlans",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPlans_UserId",
                table: "UserPlans",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserPlans_Plans_PlanId",
                table: "UserPlans",
                column: "PlanId",
                principalTable: "Plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPlans_Users_UserId",
                table: "UserPlans",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPlans_Plans_PlanId",
                table: "UserPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPlans_Users_UserId",
                table: "UserPlans");

            migrationBuilder.DropIndex(
                name: "IX_UserPlans_PlanId",
                table: "UserPlans");

            migrationBuilder.DropIndex(
                name: "IX_UserPlans_UserId",
                table: "UserPlans");
        }
    }
}
