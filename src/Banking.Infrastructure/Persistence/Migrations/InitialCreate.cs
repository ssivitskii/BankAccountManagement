using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Banking.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BankingDbContext))]
[Migration("202609040001_InitialCreate")]
public sealed class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                PasswordHash = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", row => row.Id);
                table.CheckConstraint("CK_Users_Role", "\"Role\" IN ('Customer', 'Admin')");
            });

        migrationBuilder.CreateTable(
            name: "Accounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                Number = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                Balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Accounts", row => row.Id);
                table.CheckConstraint("CK_Accounts_Balance", "\"Balance\" >= 0");
                table.ForeignKey(
                    name: "FK_Accounts_Users_OwnerId",
                    column: row => row.OwnerId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Operations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Operations", row => row.Id);
                table.CheckConstraint("CK_Operations_Amount", "\"Amount\" > 0");
                table.ForeignKey(
                    name: "FK_Operations_Accounts_AccountId",
                    column: row => row.AccountId,
                    principalTable: "Accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_Users_Username", table: "Users", column: "Username", unique: true);
        migrationBuilder.CreateIndex(name: "IX_Accounts_Number", table: "Accounts", column: "Number", unique: true);
        migrationBuilder.CreateIndex(name: "IX_Accounts_OwnerId", table: "Accounts", column: "OwnerId");
        migrationBuilder.CreateIndex(
            name: "IX_Operations_AccountId_OccurredAt",
            table: "Operations",
            columns: ["AccountId", "OccurredAt"]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Operations");
        migrationBuilder.DropTable(name: "Accounts");
        migrationBuilder.DropTable(name: "Users");
    }
}
