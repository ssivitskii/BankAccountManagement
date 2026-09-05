using Banking.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts", table => table.HasCheckConstraint("CK_Accounts_Balance", "\"Balance\" >= 0"));
        builder.HasKey(account => account.Id);
        builder.Property(account => account.Number).HasMaxLength(34).IsRequired();
        builder.Property(account => account.Balance)
            .HasConversion(money => money.Amount, amount => new Money(amount))
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder.HasIndex(account => account.Number).IsUnique();
        builder.HasIndex(account => account.OwnerId);
        builder.HasOne<User>().WithMany().HasForeignKey(account => account.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
