using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(record => new { record.ActorId, record.Scope, record.KeyHash });
        builder.Property(record => record.Scope).HasMaxLength(40).IsRequired();
        builder.Property(record => record.KeyHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(record => record.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(record => record.CreatedAt).IsRequired();
        builder.HasIndex(record => record.TransferId);
    }
}
