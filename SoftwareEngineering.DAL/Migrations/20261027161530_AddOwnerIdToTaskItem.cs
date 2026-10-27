using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareEngineering.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerIdToTaskItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "tasks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Існуючі завдання отримують власника: автора (якщо він є серед користувачів) або власника проєкту.
            migrationBuilder.Sql(@"
                UPDATE tasks t
                SET owner_id = CASE
                    WHEN EXISTS (SELECT 1 FROM users u WHERE u.id = t.creator_id) THEN t.creator_id
                    ELSE p.owner_id
                END
                FROM projects p
                WHERE p.id = t.project_id;

                UPDATE tasks
                SET creator_id = owner_id
                WHERE creator_id = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_owner_id",
                table: "tasks",
                column: "owner_id");

            migrationBuilder.AddForeignKey(
                name: "fk_tasks_users_owner_id",
                table: "tasks",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tasks_users_owner_id",
                table: "tasks");

            migrationBuilder.DropIndex(
                name: "ix_tasks_owner_id",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "tasks");
        }
    }
}
