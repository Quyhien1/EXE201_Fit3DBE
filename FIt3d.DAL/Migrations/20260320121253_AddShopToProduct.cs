using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FIt3d.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddShopToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Shop",
                schema: "fit3d",
                table: "Products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "fit3d",
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("3319290d-7bc6-4f66-955e-0bff16902e4f"),
                column: "Shop",
                value: null);

            migrationBuilder.UpdateData(
                schema: "fit3d",
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("78575687-a6be-47fe-9fd9-7fa0dec32b66"),
                column: "Shop",
                value: null);

            migrationBuilder.UpdateData(
                schema: "fit3d",
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a2561418-c8a0-4451-9caf-2cc58c625d1e"),
                column: "Shop",
                value: null);

            migrationBuilder.UpdateData(
                schema: "fit3d",
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("cb2e4c53-0754-487d-9bc2-8a8e5a5a8ff7"),
                column: "Shop",
                value: null);

            migrationBuilder.UpdateData(
                schema: "fit3d",
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("d26859fa-798d-4341-b8c7-a2760b4b7522"),
                column: "Shop",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Shop",
                schema: "fit3d",
                table: "Products");
        }
    }
}
