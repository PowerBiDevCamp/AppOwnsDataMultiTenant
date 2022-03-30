using AppOwnsDataMultiTenant.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.PowerBI.Api.Models;

namespace AppOwnsDataMultiTenant.Services {

  public class CustomerTenantManager {

    private PowerBiServiceApi powerBiServiceApi;
    private AppOwnsDataMultiTenantDbService appOwnsDataMultiTenantDbService;

    public CustomerTenantManager(PowerBiServiceApi powerBiServiceApi, AppOwnsDataMultiTenantDbService appOwnsDataMultiTenantDbService) {
      this.powerBiServiceApi = powerBiServiceApi;
      this.appOwnsDataMultiTenantDbService = appOwnsDataMultiTenantDbService;
    }

    public IList<AppProfile> GetAppProfiles() {
      return this.appOwnsDataMultiTenantDbService.GetProfiles();
    }

    public AppProfile GetAppProfile(string ProfileName) {
      return this.appOwnsDataMultiTenantDbService.GetProfile(ProfileName);
    }

    public IList<ServicePrincipalProfile> GetPowerBiProfiles() {
      return this.powerBiServiceApi.GetProfiles();
    }

    public void DeleteProfile(string ProfileId) {
      this.powerBiServiceApi.DeleteProfile(ProfileId);
      this.appOwnsDataMultiTenantDbService.DeleteProfile(ProfileId);
    }

    public string GetNextProfileName() {
      return this.appOwnsDataMultiTenantDbService.GetNextProfileName();
    }

    public AppProfile CreateProfile(string ProfileName, bool Exclusive = false) {

      ServicePrincipalProfile servicePrincipalProfile = this.powerBiServiceApi.CreateProfile(ProfileName);

      AppProfile appProfile = new AppProfile {
        ProfileId = servicePrincipalProfile.Id.ToString(),
        ProfileName = ProfileName,
        Created = DateTime.Now,
        Exclusive = Exclusive
      };

      return this.appOwnsDataMultiTenantDbService.CreateProfile(appProfile);
    }

    public IList<CustomerTenant> GetTenants() {
      return this.appOwnsDataMultiTenantDbService.GetTenants();
    }

    public CustomerTenantDetails GetTenantDetails(string TenantName) {
      var tenant = appOwnsDataMultiTenantDbService.GetTenant(TenantName);
      return powerBiServiceApi.GetTenantDetails(tenant);
    }

    public class OnboardTenantModel {
      public string TenantName { get; set; }
      public string SuggestedDatabase { get; set; }
      public List<SelectListItem> DatabaseOptions { get; set; }
      public List<SelectListItem> ProfileOptions { get; set; }
    }

    public OnboardTenantModel GetOnboardTenantModel() {

      string suggestedTenantName = this.appOwnsDataMultiTenantDbService.GetNextTenantName();
      var profilesInPool = this.appOwnsDataMultiTenantDbService.GetProfilesInPool();

      var model = new OnboardTenantModel {
        TenantName = suggestedTenantName,
        ProfileOptions = profilesInPool.Select(profile => new SelectListItem {
          Text = profile,
          Value = profile
        }).ToList(),
        DatabaseOptions = new List<SelectListItem> {
          new SelectListItem{ Text="AcmeCorpSales", Value="AcmeCorpSales" },
          new SelectListItem{ Text="ContosoSales", Value="ContosoSales" },
          new SelectListItem{ Text="MegaCorpSales", Value="MegaCorpSales" }
        },
        SuggestedDatabase = "WingtipSales"
      };

      return model;

    }

    public void OnboardTenant(string TenantName, string DatabaseServer, string DatabaseName, string DatabaseUserName, string DatabaseUserPassword, string ProfileName, string Exclusive) {

      if (string.IsNullOrEmpty(ProfileName)) {
        ProfileName = TenantName;
      }

      var tenant = new CustomerTenant {
        Name = TenantName,
        DatabaseServer = DatabaseServer,
        DatabaseName = DatabaseName,
        DatabaseUserName = DatabaseUserName,
        DatabaseUserPassword = DatabaseUserPassword,
        ProfileName = ProfileName
      };

      if (Exclusive.Equals("True")) {
        AppProfile profile = CreateProfile(TenantName, true);
        tenant.Profile = profile;
      }
      else {
        tenant.Profile = GetAppProfile(tenant.ProfileName);
      }

      tenant = this.powerBiServiceApi.OnboardNewTenant(tenant);
      tenant.Created = DateTime.Now.AddHours(0); // no time offset for local dev
      this.appOwnsDataMultiTenantDbService.OnboardNewTenant(tenant);

    }

    public void DeleteTenant(string TenantName) {

      var tenant = this.appOwnsDataMultiTenantDbService.GetTenant(TenantName);
      
      bool exclusiveProfile = tenant.Profile.Exclusive;
      string profileId = tenant.Profile.ProfileId;

      this.powerBiServiceApi.DeleteWorkspace(tenant);
      this.appOwnsDataMultiTenantDbService.DeleteTenant(tenant);

      if(exclusiveProfile) {
        DeleteProfile(profileId);
      }

    }

    public EmbeddedReportViewModel GetEmbeddedReportViewModel(string TenantName) {
      var tenant = this.appOwnsDataMultiTenantDbService.GetTenant(TenantName);
      return this.powerBiServiceApi.GetReportEmbeddingData(tenant).Result;
    }

  }
}