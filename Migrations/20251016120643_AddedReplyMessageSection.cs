using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealtimeChat.Migrations
{
    /// <inheritdoc />
    public partial class AddedReplyMessageSection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_AspNetUsers_FromUserId",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_AspNetUsers_ToUserId",
                table: "FriendRequests");

            migrationBuilder.AddColumn<int>(
                name: "ReplyToMessageId",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReplyToMessageId",
                table: "Messages",
                column: "ReplyToMessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_AspNetUsers_FromUserId",
                table: "FriendRequests",
                column: "FromUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_AspNetUsers_ToUserId",
                table: "FriendRequests",
                column: "ToUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Messages_ReplyToMessageId",
                table: "Messages",
                column: "ReplyToMessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_AspNetUsers_FromUserId",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_AspNetUsers_ToUserId",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Messages_ReplyToMessageId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ReplyToMessageId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                table: "Messages");

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_AspNetUsers_FromUserId",
                table: "FriendRequests",
                column: "FromUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_AspNetUsers_ToUserId",
                table: "FriendRequests",
                column: "ToUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
