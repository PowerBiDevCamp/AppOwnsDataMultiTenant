using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AppOwnsDataMultiTenant.Models {

  public class AppProfile {
    [Key]
    public string ProfileName { get; set; }
    public string ProfileId { get; set; }
    public bool Exclusive { get; set; }
    public DateTime Created { get; set; }
    public virtual List<CustomerTenant> Tenants { get; set; }
  }

  public class CustomerTenant {
    [Key]
    public string Name { get; set; }
    public string WorkspaceId { get; set; }
    public string WorkspaceUrl { get; set; }
    public string DatabaseServer { get; set; }
    public string DatabaseName { get; set; }
    public string DatabaseUserName { get; set; }
    public string DatabaseUserPassword { get; set; }
    public DateTime Created { get; set; }
    public string ProfileName { get; set; }
    public AppProfile Profile { get; set; }
  }

  public class AppOwnsDataMultiTenantDB : DbContext {

    public AppOwnsDataMultiTenantDB(DbContextOptions<AppOwnsDataMultiTenantDB> options)
    : base(options) { }

    public DbSet<AppProfile> Profiles { get; set; }
    public DbSet<CustomerTenant> Tenants { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {

      modelBuilder.Entity<CustomerTenant>()
            .HasOne(tenant => tenant.Profile)
            .WithMany(profile => profile.Tenants)
            .HasForeignKey(tenant => tenant.ProfileName);

    }

  }

}
