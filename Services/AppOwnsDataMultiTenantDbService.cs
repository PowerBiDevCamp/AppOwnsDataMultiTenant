using System.Collections.Generic;
using System.Linq;
using AppOwnsDataMultiTenant.Models;
using Microsoft.PowerBI.Api.Models;

namespace AppOwnsDataMultiTenant.Services {

  public class AppOwnsDataMultiTenantDbService {

    private readonly AppOwnsDataMultiTenantDB dbContext;

    public AppOwnsDataMultiTenantDbService(AppOwnsDataMultiTenantDB context) {
      dbContext = context;
    }

    public AppProfile CreateProfile(AppProfile Profile) {      
      dbContext.Profiles.Add(Profile);
      dbContext.SaveChanges();
      return Profile;
    }

    public IList<AppProfile> GetProfiles() {

      // get app identity
      var profiles = dbContext.Profiles
                       .Select(Profile => Profile)
                       .OrderBy(Profile => Profile.ProfileName)
                       .ToList();

      // populate Tenants collection
      foreach (var profile in profiles) {
        profile.Tenants =
          dbContext.Tenants.Where(tenant => tenant.Profile.ProfileName == profile.ProfileName).ToList();
      }

      return profiles;
    }

    public IList<string> GetProfilesInPool() {

      // get app identity
      var profiles = dbContext.Profiles
                       .Select(Profile => Profile)
                       .Where(Profile => Profile.Exclusive == false)
                       .OrderBy(Profile => Profile.ProfileName)
                       .ToList();

      return profiles.Select(Profile => Profile.ProfileName).ToList();
    }

    public AppProfile GetProfile(string ProfileName) {
      var profile = dbContext.Profiles.Where(profile => profile.ProfileName == ProfileName).First();
      profile.Tenants = dbContext.Tenants.Where(tenant => tenant.ProfileName == ProfileName).ToList();
      return profile;
    }

    public void DeleteProfile(string ProfileId) {
      var servicePrincipalProfile = dbContext.Profiles.Where(profile => profile.ProfileId == ProfileId).First();
      dbContext.Profiles.Remove(servicePrincipalProfile);
      dbContext.SaveChanges();
      return;
    }

    public string GetNextProfileName() {
      var appNames = dbContext.Profiles.Select(servicePrincipalProfile => servicePrincipalProfile.ProfileName).ToList();
      string baseName = "GenericProfile";
      string nextName;
      int counter = 0;
      do {
        counter += 1;
        nextName = baseName + counter.ToString("00");
      }
      while (appNames.Contains(nextName));
      return nextName;
    }

    public string GetNextTenantName() {
      var appNames = dbContext.Tenants.Select(tenant => tenant.Name).ToList();
      string baseName = "CustomerTenant";
      string nextName;
      int counter = 0;
      do {
        counter += 1;
        nextName = baseName + counter.ToString("00");
      }
      while (appNames.Contains(nextName));
      return nextName;
    }

    public AppProfile GetNextProfileInPool() {
      var AppOwnsDataMultiTenant = GetProfiles().Where(servicePrincipalProfile => servicePrincipalProfile.Exclusive == false);
      if (AppOwnsDataMultiTenant.Count() == 0) {
        return null;
      }
      IList<int> counts = AppOwnsDataMultiTenant.Select(servicePrincipalProfile => servicePrincipalProfile.Tenants.Count()).ToList();
      int minCount = counts.Min();
      return AppOwnsDataMultiTenant.Where(servicePrincipalProfile => servicePrincipalProfile.Tenants.Count() == minCount).First();
    }

    public void OnboardNewTenant(CustomerTenant tenant) {
      dbContext.Tenants.Add(tenant);
      dbContext.SaveChanges();
    }

    public IList<CustomerTenant> GetTenants() {
      return dbContext.Tenants
             .Select(tenant => tenant).OrderBy(tenant => tenant.Profile)
             .OrderBy(tenant => tenant.Name).ToList();
    }

    public CustomerTenant GetTenant(string TenantName) {
      var tenant = dbContext.Tenants.Where(tenant => tenant.Name == TenantName).First();
      tenant.Profile = dbContext.Profiles.Where(profile => profile.ProfileName == tenant.ProfileName).First();
      return tenant;
    }

    public Boolean TenantAlreadyExists(string TenantName) {
      var response = dbContext.Tenants.Where(tenant => tenant.Name == TenantName);
      return (response.Count() > 0);
    }

    public void DeleteTenant(CustomerTenant tenant) {
      dbContext.Tenants.Remove(tenant);
      dbContext.SaveChanges();
      return;
    }

  }

}
