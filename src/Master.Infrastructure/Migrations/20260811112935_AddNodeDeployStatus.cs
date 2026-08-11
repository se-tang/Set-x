using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNodeDeployStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeployError",
                table: "Nodes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeployStatus",
                table: "Nodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeployedAt",
                table: "Nodes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NodeUserBindings_NodeId",
                table: "NodeUserBindings",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_NodeUserBindings_UserId",
                table: "NodeUserBindings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_ServerId",
                table: "Nodes",
                column: "ServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Nodes_Servers_ServerId",
                table: "Nodes",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NodeUserBindings_Nodes_NodeId",
                table: "NodeUserBindings",
                column: "NodeId",
                principalTable: "Nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NodeUserBindings_Users_UserId",
                table: "NodeUserBindings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Nodes_Servers_ServerId",
                table: "Nodes");

            migrationBuilder.DropForeignKey(
                name: "FK_NodeUserBindings_Nodes_NodeId",
                table: "NodeUserBindings");

            migrationBuilder.DropForeignKey(
                name: "FK_NodeUserBindings_Users_UserId",
                table: "NodeUserBindings");

            migrationBuilder.DropIndex(
                name: "IX_NodeUserBindings_NodeId",
                table: "NodeUserBindings");

            migrationBuilder.DropIndex(
                name: "IX_NodeUserBindings_UserId",
                table: "NodeUserBindings");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_ServerId",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "DeployError",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "DeployStatus",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "DeployedAt",
                table: "Nodes");
        }
    }
}
