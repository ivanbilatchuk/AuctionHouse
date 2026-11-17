using Catalog.Dal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

// dbo.ProcessedEvents: PRIMARY KEY (EventId) гарантує, що подію не буде позначено обробленою двічі
// навіть за конкурентної повторної доставки (порушення PK 2627 = «уже оброблено»).
public sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.ToTable("ProcessedEvents", "dbo");

        builder.HasKey(e => e.EventId).HasName("PK_ProcessedEvents");
        builder.Property(e => e.EventId).ValueGeneratedNever();
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(200);
        builder.Property(e => e.ProcessedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");

        // Для періодичного очищення старих записів (подія старша за термін можливої повторної доставки).
        builder.HasIndex(e => e.ProcessedAt).HasDatabaseName("IX_ProcessedEvents_ProcessedAt");
    }
}
