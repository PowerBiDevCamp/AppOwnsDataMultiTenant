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

    private CustomerTenantManager tenantManager;
  
    public HomeController(CustomerTenantManager tenantManager, PowerBiServiceApi powerBiServiceApi, AppOwnsDataMultiTenantDbService appOwnsDataMultiTenantDbService) {
      this.tenantManager = tenantManager;
    }

    public IActionResult Index() {
      return View();
    }

    public IActionResult Profiles() {
      var viewModel = this.tenantManager.GetAppProfiles();
      return View(viewModel);
    }

    public IActionResult PowerBiProfiles() {
      var viewModel = this.tenantManager.GetPowerBiProfiles();
      return View(viewModel);
    }

    public IActionResult Profile(string ProfileName) {
      var viewModel = this.tenantManager.GetAppProfile(ProfileName);
      return View(viewModel);
    }

    public IActionResult DeleteProfile(string ProfileId) {
      this.tenantManager.DeleteProfile(ProfileId);
      return RedirectToAction("Profiles");
    }

    public class CreateProfileViewModel {
      public string ProfileName { get; set; }
    }

    public IActionResult CreateProfile() {
      var model = new CreateProfileViewModel {
        ProfileName = this.tenantManager.GetNextProfileName()
      };
      return View(model);
    }

    [HttpPost]
    public IActionResult CreateProfile(string ProfileName) {
      this.tenantManager.CreateProfile(ProfileName);
      return RedirectToAction("Profiles");
    }

    public IActionResult Tenants() {
      var model = this.tenantManager.GetTenants();
      return View(model);
    }

    public IActionResult Tenant(string TenantName) {
      var tenantDetails = this.tenantManager.GetTenantDetails(TenantName);
      return View(tenantDetails);
    }

    public IActionResult OnboardTenant() {
      var model = this.tenantManager.GetOnboardTenantModel();
      return View(model);
    }

    [HttpPost]
    public IActionResult OnboardTenant(string TenantName, string DatabaseServer, string DatabaseName, string DatabaseUserName, string DatabaseUserPassword, string ProfileName, string Exclusive) {
      this.tenantManager.OnboardTenant(TenantName, DatabaseServer, DatabaseName, DatabaseUserName, DatabaseUserPassword, ProfileName, Exclusive);
      return RedirectToAction("Tenants");
    }

    public IActionResult DeleteTenant(string TenantName) {
      this.tenantManager.DeleteTenant(TenantName);
      return RedirectToAction("Tenants");
    }

    public IActionResult Embed(string TenantName) {
      var model = this.tenantManager.GetEmbeddedReportViewModel(TenantName);
      return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() {
      return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
  }
}