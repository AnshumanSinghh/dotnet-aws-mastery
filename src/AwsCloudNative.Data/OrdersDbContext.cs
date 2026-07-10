using AwsCloudNative.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AwsCloudNative.Data
{
    /// <summary>
    /// EF Core DbContext for the Orders domain.
    /// Represents a single unit of work and a repository for OrderEntity.
    ///
    /// WHY one DbContext per domain (not one per table):
    /// DbContext is the unit of work boundary. All changes tracked within
    /// one DbContext instance are committed or rolled back together in
    /// one database transaction via SaveChangesAsync.
    ///
    /// PITFALL — DbContext lifetime in ASP.NET Core:
    /// DbContext must be registered as Scoped — one instance per HTTP request.
    /// Never register as Singleton: DbContext is not thread-safe.
    /// Never register as Transient: creates a new connection per resolve — wasteful.
    /// </summary>
    public sealed class OrdersDbContext : DbContext
    {
        /// <summary>
        /// Initialises OrdersDbContext with injected options.
        /// Options carry the connection string and provider configuration
        /// registered in DataExtensions.AddProductionDatabase.
        /// </summary>
        public OrdersDbContext(DbContextOptions<OrdersDbContext> options)
            : base(options) { }

        /// <summary>
        /// DbSet representing the orders table in the orders schema.
        /// Used for querying and persisting OrderEntity instances.
        /// </summary>
        public DbSet<OrderEntity> Orders => Set<OrderEntity>();

        /// <summary>
        /// Applies all IEntityTypeConfiguration implementations from this assembly.
        /// WHY ApplyConfigurationsFromAssembly:
        /// Automatically discovers all configuration classes (like OrderEntityConfiguration)
        /// without manually calling builder.ApplyConfiguration() for each one.
        /// New entities added to the project are picked up automatically.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);
        }
    }
}
