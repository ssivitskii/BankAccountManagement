using Banking.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(
            "Users",
            table => table.HasCheckConstraint("CK_Users_Role", "\"Role\" IN ('Customer', 'Admin')"));
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Username).HasMaxLength(100).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(1000).IsRequired();
        builder.Property(user => user.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(user => user.Username).IsUnique();
    }
}
