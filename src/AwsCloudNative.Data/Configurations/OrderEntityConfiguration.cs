using AwsCloudNative.Common.Constants;
using AwsCloudNative.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AwsCloudNative.Data.Configurations
{
    /// <summary>
    /// Fluent API (Application Programming Interface) configuration for OrderEntity.
    /// WHY IEntityTypeConfiguration over DataAnnotations:
    /// Keeps entity classes clean — no EF Core attributes mixed with domain properties.
    /// All persistence concerns (table names, indexes, constraints) are isolated here.
    /// This class is automatically discovered by EF Core when using
    /// modelBuilder.ApplyConfigurationsFromAssembly().
    /// </summary>
    public sealed class OrderEntityConfiguration : IEntityTypeConfiguration<OrderEntity>
    {
        public void Configure(EntityTypeBuilder<OrderEntity> builder)
        {
            // Explicit schema and table name from constants — never magic strings.
            builder.ToTable(RdsConstants.Tables.Orders, RdsConstants.OrdersSchema);

            // Primary key — application-generated UUID.
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Id)
                .ValueGeneratedNever(); // Application sets this — not the database

            // OrderId — unique business key, indexed for fast lookup.
            builder.Property(o => o.OrderId)
                .IsRequired()
                .HasMaxLength(100);
            builder.HasIndex(o => o.OrderId)
                .IsUnique()
                .HasDatabaseName("ix_orders_order_id");

            // CustomerId — indexed for querying orders by customer.
            builder.Property(o => o.CustomerId)
                .IsRequired()
                .HasMaxLength(200);
            builder.HasIndex(o => o.CustomerId)
                .HasDatabaseName("ix_orders_customer_id");

            // Amount — PostgreSQL numeric with explicit precision.
            // WHY: EF Core defaults decimal to numeric(18,2) on PostgreSQL.
            // Explicit precision prevents silent truncation on large amounts.
            builder.Property(o => o.Amount)
                .HasPrecision(18, 4)
                .IsRequired();

            // Status — stored as varchar, not integer.
            builder.Property(o => o.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue(OrderStatus.Pending);

            // Timestamps — stored as timestamptz (timestamp with timezone) in PostgreSQL.
            // WHY timestamptz: Stores UTC offset in the database.
            // timestamptz is always stored in UTC internally — no timezone confusion.
            builder.Property(o => o.CreatedAtUtc)
                .IsRequired()
                .HasColumnType("timestamptz");

            builder.Property(o => o.UpdatedAtUtc)
                .HasColumnType("timestamptz");

            // builder.UseXminAsConcurrencyToken(); ===> DEPRECATED
            
            // Optimistic concurrency via xmin system column.
            // WHY xmin: PostgreSQL's internal row version column updates
            // automatically on every row write — no manual management needed.
            // EF Core + Npgsql natively supports xmin as a concurrency token.
            builder.Property(o => o.RowVersion)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        }
    }
}
