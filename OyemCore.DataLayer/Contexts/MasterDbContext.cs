using Microsoft.EntityFrameworkCore;
using OyemCore.DataLayer.Entities;

namespace OyemCore.DataLayer.Contexts
{
    public class MasterDbContext : DbContext
    {
        public MasterDbContext(DbContextOptions<MasterDbContext> options) : base(options)
        {
        }

        public DbSet<Tenant> Tenants { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<Tenant>().ToTable("Tenant").HasKey(e => e.TenantId);
        }
    }
}
