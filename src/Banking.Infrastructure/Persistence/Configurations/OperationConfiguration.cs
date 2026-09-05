using Banking.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Persistence.Configurations;

public sealed class OperationConfiguration : IEntityTypeConfiguration<Operation>
{
    public void Configure(EntityTypeBuilder<Operation> builder)
    {
        const string transferLinkConstraint =
            "(\"Type\" IN ('TransferOut', 'TransferIn') AND \"TransferId\" IS NOT NULL) OR " +
            "(\"Type\" IN ('Deposit', 'Withdrawal') AND \"TransferId\" IS NULL)";
        builder.ToTable(
            "Operations",
            table =>
            {
                table.HasCheckConstraint("CK_Operations_Amount", "\"Amount\" > 0");
                table.HasCheckConstraint(
                    "CK_Operations_TransferLink",
                    transferLinkConstraint);
            });
        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(operation => operation.Amount)
            .HasConversion(money => money.Amount, amount => new Money(amount))
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder.Property(operation => operation.OccurredAt).IsRequired();
        builder.HasIndex(operation => new { operation.AccountId, operation.OccurredAt, operation.Id });
        builder.HasIndex(operation => new { operation.TransferId, operation.Type })
            .IsUnique()
            .HasFilter("\"TransferId\" IS NOT NULL");
        builder.HasOne<Account>().WithMany().HasForeignKey(operation => operation.AccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Transfer>().WithMany().HasForeignKey(operation => operation.TransferId).OnDelete(DeleteBehavior.Restrict);
    }
}
