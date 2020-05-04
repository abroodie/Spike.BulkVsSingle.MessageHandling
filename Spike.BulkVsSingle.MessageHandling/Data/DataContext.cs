using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Spike.BulkVsSingle.MessageHandling.Data.Configurations;
using Spike.BulkVsSingle.MessageHandling.Data.Entities;

namespace Spike.BulkVsSingle.MessageHandling.Data
{
    public class DataContext: DbContext
    {
        protected string ConnectionString { get; }

        public virtual DbSet<Payment> Payment { get; set; }


        public DataContext(string connectionString)
        {
            ConnectionString = connectionString;
        }


        public DataContext(DbContextOptions<DataContext> options) : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema("Payments2");
            modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (ConnectionString != null)
                optionsBuilder.UseSqlServer(ConnectionString);
        }

        public async Task<int> SaveChanges(CancellationToken cancellationToken = default(CancellationToken))
        {
            return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}