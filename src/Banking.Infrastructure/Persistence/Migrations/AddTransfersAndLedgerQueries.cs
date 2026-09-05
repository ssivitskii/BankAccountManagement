using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Banking.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BankingDbContext))]
[Migration("202609040002_AddTransfersAndLedgerQueries")]
public sealed class AddTransfersAndLedgerQueries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        const string transferLinkConstraint =
            "(\"Type\" IN ('TransferOut', 'TransferIn') AND \"TransferId\" IS NOT NULL) OR " +
            "(\"Type\" IN ('Deposit', 'Withdrawal') AND \"TransferId\" IS NULL)";
        migrationBuilder.CreateTable(
            name: "Transfers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SourceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                DestinationAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                InitiatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Transfers", row => row.Id);
                table.CheckConstraint("CK_Transfers_Amount", "\"Amount\" > 0");
                table.CheckConstraint(
                    "CK_Transfers_DistinctAccounts",
                    "\"SourceAccountId\" <> \"DestinationAccountId\"");
                table.ForeignKey(
                    name: "FK_Transfers_Accounts_DestinationAccountId",
                    column: row => row.DestinationAccountId,
                    principalTable: "Accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Transfers_Accounts_SourceAccountId",
                    column: row => row.SourceAccountId,
                    principalTable: "Accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Transfers_Users_InitiatedByUserId",
                    column: row => row.InitiatedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "IdempotencyRecords",
            columns: table => new
            {
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                Scope = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                KeyHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                RequestHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                TransferId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table => table.PrimaryKey(
                "PK_IdempotencyRecords",
                row => new { row.ActorId, row.Scope, row.KeyHash }));

        migrationBuilder.AddColumn<Guid>(
            name: "TransferId",
            table: "Operations",
            type: "uuid",
            nullable: true);
        migrationBuilder.DropIndex(name: "IX_Operations_AccountId_OccurredAt", table: "Operations");
        migrationBuilder.AddCheckConstraint(
            name: "CK_Operations_TransferLink",
            table: "Operations",
            sql: transferLinkConstraint);
        migrationBuilder.CreateIndex(
            name: "IX_IdempotencyRecords_TransferId",
            table: "IdempotencyRecords",
            column: "TransferId");
        migrationBuilder.CreateIndex(
            name: "IX_Operations_AccountId_OccurredAt_Id",
            table: "Operations",
            columns: ["AccountId", "OccurredAt", "Id"]);
        migrationBuilder.CreateIndex(
            name: "IX_Operations_TransferId_Type",
            table: "Operations",
            columns: ["TransferId", "Type"],
            unique: true,
            filter: "\"TransferId\" IS NOT NULL");
        migrationBuilder.CreateIndex(
            name: "IX_Transfers_DestinationAccountId_OccurredAt",
            table: "Transfers",
            columns: ["DestinationAccountId", "OccurredAt"]);
        migrationBuilder.CreateIndex(
            name: "IX_Transfers_InitiatedByUserId",
            table: "Transfers",
            column: "InitiatedByUserId");
        migrationBuilder.CreateIndex(
            name: "IX_Transfers_SourceAccountId_OccurredAt",
            table: "Transfers",
            columns: ["SourceAccountId", "OccurredAt"]);
        migrationBuilder.AddForeignKey(
            name: "FK_Operations_Transfers_TransferId",
            table: "Operations",
            column: "TransferId",
            principalTable: "Transfers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Operations_Transfers_TransferId", table: "Operations");
        migrationBuilder.DropTable(name: "IdempotencyRecords");
        migrationBuilder.DropTable(name: "Transfers");
        migrationBuilder.DropCheckConstraint(name: "CK_Operations_TransferLink", table: "Operations");
        migrationBuilder.DropIndex(name: "IX_Operations_AccountId_OccurredAt_Id", table: "Operations");
        migrationBuilder.DropIndex(name: "IX_Operations_TransferId_Type", table: "Operations");
        migrationBuilder.DropColumn(name: "TransferId", table: "Operations");
        migrationBuilder.CreateIndex(
            name: "IX_Operations_AccountId_OccurredAt",
            table: "Operations",
            columns: ["AccountId", "OccurredAt"]);
    }
}
