using Banking.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Persistence.Configurations;

public sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable(
            "Transfers",
            table =>
            {
                table.HasCheckConstraint("CK_Transfers_Amount", "\"Amount\" > 0");
                table.HasCheckConstraint(
                    "CK_Transfers_DistinctAccounts",
                    "\"SourceAccountId\" <> \"DestinationAccountId\"");
            });
        builder.HasKey(transfer => transfer.Id);
        builder.Property(transfer => transfer.Amount)
            .HasConversion(money => money.Amount, amount => new Money(amount))
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder.Property(transfer => transfer.OccurredAt).IsRequired();
        builder.HasIndex(transfer => new { transfer.SourceAccountId, transfer.OccurredAt });
        builder.HasIndex(transfer => new { transfer.DestinationAccountId, transfer.OccurredAt });
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(transfer => transfer.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(transfer => transfer.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(transfer => transfer.InitiatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
