using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecAll.Contrib.MaskedTextList.Api.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "MaskedTextList");

            migrationBuilder.CreateSequence(
                name: "MaskedTextItemSeq",
                schema: "MaskedTextList",
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "MaskedTextItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaskedContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserIdentityGuid = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaskedTextItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaskedTextItems_ItemId",
                table: "MaskedTextItems",
                column: "ItemId",
                unique: true,
                filter: "[ItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MaskedTextItems_UserIdentityGuid",
                table: "MaskedTextItems",
                column: "UserIdentityGuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaskedTextItems");

            migrationBuilder.DropSequence(
                name: "MaskedTextItemSeq",
                schema: "MaskedTextList");
        }
    }
}
