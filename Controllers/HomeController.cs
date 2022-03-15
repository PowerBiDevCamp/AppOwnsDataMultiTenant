using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using AppOwnsDataMultiTenant.Models;
using AppOwnsDataMultiTenant.Services;

namespace AppOwnsDataMultiTenant.Controllers {

  [AllowAnonymous]
  public class HomeController : Controller {

    private PowerBiServiceApi powerBiServiceApi;
    private AppOwnsDataMultiTenantDbService appOwnsDataMultiTenantDbService;

    public HomeController(PowerBiServiceApi powerBiServiceApi, AppOwnsDataMultiTenantDbService appOwnsDataMultiTenantDbService) {
      this.powerBiServiceApi = powerBiServiceApi;
      this.appOwnsDataMultiTenantDbService = appOwnsDataMultiTenantDbService;
    }

    public IActionResult Index() {
      return View();
    }

    public IActionResult Profiles() {

      var viewModel = this.appOwnsDataMultiTenantDbService.GetProfiles();
      return View(viewModel);
    }

    public IActionResult PowerBiProfiles() {

      var viewModel = this.powerBiServiceApi.GetProfiles();
      return View(viewModel);
    }

    public IActionResult Profile(string ProfileName) {
      var viewModel = this.appOwnsDataMultiTenantDbService.GetProfile(ProfileName);
      return View(viewModel);
    }

    public IActionResult DeleteProfile(string ProfileId) {
      this.powerBiServiceApi.DeleteProfile(ProfileId);
      this.appOwnsDataMultiTenantDbService.DeleteProfile(ProfileId);
      return RedirectToAction("Profiles");
    }

    public class CreateServicePrincipalProfileModel {
      public string ProfileName { get; set; }
    }

    public IActionResult CreateProfile() {
      var model = new CreateServicePrincipalProfileModel {
        ProfileName = this.appOwnsDataMultiTenantDbService.GetNextProfileName()
      };
      return View(model);
    }

    [HttpPost]
    public IActionResult CreateProfile(string ProfileName) {
      ServicePrincipalProfile servicePrincipalProfile = this.powerBiServiceApi.CreateProfile(ProfileName);
      this.appOwnsDataMultiTenantDbService.CreateProfile(servicePrincipalProfile);
      return RedirectToAction("Profiles");
    }

    public IActionResult Tenants() {
      var model = this.appOwnsDataMultiTenantDbService.GetTenants();
      return View(model);
    }

    public IActionResult Tenant(string Name) {
      var model = appOwnsDataMultiTenantDbService.GetTenant(Name);
      powerBiServiceApi.SetCallingContext(model.ProfileName);
      var modelWithDetails = powerBiServiceApi.GetTenantDetails(model);
      return View(modelWithDetails);
    }

    public class OnboardTenantModel {
      public string TenantName { get; set; }
      public string SuggestedDatabase { get; set; }
      public List<SelectListItem> DatabaseOptions { get; set; }
      public List<SelectListItem> ProfileOptions { get; set; }
    }

    public IActionResult OnboardTenant() {

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

      return View(model);
    }

    [HttpPost]
    public IActionResult OnboardTenant(string TenantName, string DatabaseServer, string DatabaseName, string DatabaseUserName, string DatabaseUserPassword, string ProfileName, string Exclusive) {

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
        ServicePrincipalProfile servicePrincipalProfile = this.powerBiServiceApi.CreateProfile(TenantName);
        servicePrincipalProfile.Exclusive = true;
        this.appOwnsDataMultiTenantDbService.CreateProfile(servicePrincipalProfile);
        tenant.Profile = servicePrincipalProfile;
        tenant.ProfileName = servicePrincipalProfile.Name;
      }
      else {
        tenant.Profile = this.appOwnsDataMultiTenantDbService.GetProfile(tenant.ProfileName);
      }

      tenant = this.powerBiServiceApi.OnboardNewTenant(tenant);
      tenant.Created = DateTime.Now.AddHours(0); // no time offset for local dev
      this.appOwnsDataMultiTenantDbService.OnboardNewTenant(tenant);

      return RedirectToAction("Tenants");

    }

    public IActionResult DeleteTenant(string TenantName) {
      var tenant = this.appOwnsDataMultiTenantDbService.GetTenant(TenantName);
      this.powerBiServiceApi.DeleteWorkspace(tenant);
      this.appOwnsDataMultiTenantDbService.DeleteTenant(tenant);
      return RedirectToAction("Tenants");
    }

    public IActionResult Embed(string Profile, string Tenant) {
      var viewModel = this.powerBiServiceApi.GetReportEmbeddingData(Profile, Tenant).Result;
      return View(viewModel);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() {
      return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
  }
}