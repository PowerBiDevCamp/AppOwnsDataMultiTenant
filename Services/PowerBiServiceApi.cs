using Microsoft.Identity.Web;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.PowerBI.Api.Models.Credentials;
using Microsoft.Rest;
using AppOwnsDataMultiTenant.Models;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace AppOwnsDataMultiTenant.Services {

  public class EmbeddedReportViewModel {
    public string ReportId;
    public string Name;
    public string EmbedUrl;
    public string Token;
    public string TenantName;
  }

  public class CustomerTenantDetails : CustomerTenant {
    public IList<Report> Reports { get; set; }
    public IList<Dataset> Datasets { get; set; }
    public IList<PowerBiServiceApi.WorkspaceMember> Members { get; set; }
  }

  public class PowerBiServiceApi {

    private ITokenAcquisition tokenAcquisition { get; }
    private AppOwnsDataMultiTenantDbService AppOwnsDataMultiTenantDbService;
    private string urlPowerBiServiceApiRoot { get; }
    private readonly IWebHostEnvironment Env;
    private string adminUser { get; }
    private string servicePrincipalObjectId { get; }
    private PowerBIClient pbiClient;

    public const string powerbiApiDefaultScope = "https://analysis.windows.net/powerbi/api/.default";

    public PowerBiServiceApi(IConfiguration configuration, IWebHostEnvironment env,
                             ITokenAcquisition tokenAcquisition, AppOwnsDataMultiTenantDbService AppOwnsDataMultiTenantDbService) {
      this.urlPowerBiServiceApiRoot = configuration["PowerBi:ServiceRootUrl"];
      this.adminUser = configuration["DemoSettings:AdminUser"];
      this.servicePrincipalObjectId = configuration["DemoSettings:ServicePrincipalObjectId"];
      this.Env = env;
      this.tokenAcquisition = tokenAcquisition;
      this.AppOwnsDataMultiTenantDbService = AppOwnsDataMultiTenantDbService;
      this.pbiClient = GetPowerBiClient();
    }

    public string GetAccessToken() {
      return this.tokenAcquisition.GetAccessTokenForAppAsync(powerbiApiDefaultScope).Result;
    }

    public PowerBIClient GetPowerBiClient() {
      var tokenCredentials = new TokenCredentials(GetAccessToken(), "Bearer");
      return new PowerBIClient(new Uri(urlPowerBiServiceApiRoot), tokenCredentials);
    }

    public PowerBIClient GetPowerBiClientForProfile(string ProfileId) {
      var pbiClient = GetPowerBiClient();
      pbiClient.HttpClient.DefaultRequestHeaders.Add("X-PowerBI-profile-id", ProfileId);
      return pbiClient;
    }

    public void SetCallingContext(string ProfileName = "") {

      if (ProfileName.Equals("")) {
        Console.WriteLine("Setting calling context to default profile");
        pbiClient = GetPowerBiClient();
      }
      else {
        Console.WriteLine("Setting calling context to profile " + ProfileName);
        var profile = EnsureProfileExists(ProfileName);
        pbiClient = GetPowerBiClientForProfile(profile.id);
      }

    }

    

    #region "REST operation utility methods"

    private string ExecuteGetRequest(string restUri) {

      HttpClient client = new HttpClient();
      client.DefaultRequestHeaders.Add("Authorization", "Bearer " + GetAccessToken());
      client.DefaultRequestHeaders.Add("Accept", "application/json");

      HttpResponseMessage response = client.GetAsync(restUri).Result;

      if (response.IsSuccessStatusCode) {
        return response.Content.ReadAsStringAsync().Result;
      }
      else {
        Console.WriteLine();
        Console.WriteLine("OUCH! - error occurred during GET REST call");
        Console.WriteLine();
        return string.Empty;
      }
    }

    private string ExecutePostRequest(string restUri, string postBody) {

      try {
        HttpContent body = new StringContent(postBody);
        body.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/json");
        HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + GetAccessToken());
        HttpResponseMessage response = client.PostAsync(restUri, body).Result;

        if (response.IsSuccessStatusCode) {
          return response.Content.ReadAsStringAsync().Result;
        }
        else {
          Console.WriteLine();
          Console.WriteLine("OUCH! - error occurred during POST REST call");
          Console.WriteLine();
          return string.Empty;
        }
      }
      catch {
        Console.WriteLine();
        Console.WriteLine("OUCH! - error occurred during POST REST call");
        Console.WriteLine();
        return string.Empty;
      }
    }

    private string ExecuteDeleteRequest(string restUri) {
      HttpClient client = new HttpClient();
      client.DefaultRequestHeaders.Add("Accept", "application/json");
      client.DefaultRequestHeaders.Add("Authorization", "Bearer " + GetAccessToken());
      HttpResponseMessage response = client.DeleteAsync(restUri).Result;

      if (response.IsSuccessStatusCode) {
        return response.Content.ReadAsStringAsync().Result;
      }
      else {
        Console.WriteLine();
        Console.WriteLine("OUCH! - error occurred during Delete REST call");
        Console.WriteLine();
        return string.Empty;
      }
    }

    #endregion

    #region "Profile operation utility methods"

    public class Profile {
      public string id { get; set; }
      public string displayName { get; set; }
    }

    public class ProfileCollection {
      public Profile[] value { get; set; }
    }

    public class WorkspaceMember {
      public string groupUserAccessRight { get; set; }
      public object emailAddress { get; set; }
      public object displayName { get; set; }
      public string identifier { get; set; }
      public object graphId { get; set; }
      public string principalType { get; set; }
      public Profile profile { get; set; }
    }

    public class WorkspaceMemberCollection {
      public WorkspaceMember[] value { get; set; }
    }

    private Profile CreateProfileInternal(string Name) {
      string restUri = "https://api.powerbi.com/v1.0/myorg/profiles";
      string postBody = JsonConvert.SerializeObject(new Profile { displayName = Name });
      string jsonResponse = ExecutePostRequest(restUri, postBody);

      return JsonConvert.DeserializeObject<Profile>(jsonResponse);
    }

    public ServicePrincipalProfile CreateProfile(string ProfileName) {
      var profile = CreateProfileInternal(ProfileName);
      return new ServicePrincipalProfile {
        Name = profile.displayName,
        ProfileId = profile.id,
        Created = DateTime.Now.AddHours(0) // no hour offset - used for dev
      };
    }

    public IList<Profile> GetProfiles() {
      string restUri = "https://api.powerbi.com/v1.0/myorg/profiles";
      string jsonResponse = ExecuteGetRequest(restUri);
      IList<Profile> profiles = (JsonConvert.DeserializeObject<ProfileCollection>(jsonResponse)).value;
      var filteredProfiles = profiles.Where(profile => !string.IsNullOrEmpty(profile.displayName)).ToList();
      return filteredProfiles;
    }

    public Profile GetProfile(string ProfileName) {
      var profiles = GetProfiles();
      foreach (var profile in profiles) {
        if (profile.displayName.Equals(ProfileName)) {
          return profile;
        }
      }
      return null;
    }

    public Profile EnsureProfileExists(string ProfileName) {
      var profile = GetProfile(ProfileName);
      if (profile == null) {
        profile = CreateProfileInternal(ProfileName);
      }
      return profile;
    }

    public void DisplayProfiles() {
      var profiles = GetProfiles();
      Console.WriteLine("Profiles");
      foreach (var profile in profiles) {
        Console.WriteLine(" - " + profile.displayName + " (" + profile.id + ")");
      }
      Console.WriteLine();
    }

    public void DeleteProfile(string ProfileId) {
      string restUri = "https://api.powerbi.com/v1.0/myorg/profiles/" + ProfileId;
      ExecuteDeleteRequest(restUri);
    }

    public IList<WorkspaceMember> GetWorkspaceMembers(string WorkspaceId) {
      string restUri = "https://api.powerbi.com/v1.0/myorg/groups/" + WorkspaceId + "/users";
      string jsonResponse = ExecuteGetRequest(restUri);
      return (JsonConvert.DeserializeObject<WorkspaceMemberCollection>(jsonResponse)).value;
    }

    public void AddProfileAsWorkspaceMember(string WorkspaceId, string ProfileId) {

      string restUri = "https://api.powerbi.com/v1.0/myorg/groups/" + WorkspaceId + "/users";

      WorkspaceMember newMember = new WorkspaceMember {
        groupUserAccessRight = "Admin",
        identifier = servicePrincipalObjectId,
        principalType = "App",
        profile = new Profile { id = ProfileId }
      };

      string postBody = JsonConvert.SerializeObject(newMember, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
      string jsonResponse = ExecutePostRequest(restUri, postBody);

    }

    #endregion

    public Dataset GetDataset(PowerBIClient pbiClient, Guid WorkspaceId, string DatasetName) {
      var datasets = pbiClient.Datasets.GetDatasetsInGroup(WorkspaceId).Value;
      foreach (var dataset in datasets) {
        if (dataset.Name.Equals(DatasetName)) {
          return dataset;
        }
      }
      return null;
    }

    public async Task<IList<Group>> GetTenantWorkspaces(PowerBIClient pbiClient) {
      var workspaces = (await pbiClient.Groups.GetGroupsAsync()).Value;
      return workspaces;
    }

    public CustomerTenant OnboardNewTenant(CustomerTenant tenant) {
      
      PowerBIClient pbiClient = this.GetPowerBiClient();

      // create new app workspace
      GroupCreationRequest request = new GroupCreationRequest(tenant.Name);
      Group workspace = pbiClient.Groups.CreateGroup(request);

      tenant.WorkspaceId = workspace.Id.ToString();
      tenant.WorkspaceUrl = "https://app.powerbi.com/groups/" + workspace.Id.ToString() + "/";

      // add user as new workspace admin to make demoing easier
      if (!string.IsNullOrEmpty(adminUser)) {
        pbiClient.Groups.AddGroupUser(workspace.Id, new GroupUser {
          EmailAddress = adminUser,
          GroupUserAccessRight = "Admin"
        });
      }

      // upload sample PBIX file #1
      string pbixPath = this.Env.WebRootPath + @"/PBIX/DatasetTemplate.pbix";
      string importName = "Sales";
      PublishPBIX(pbiClient, workspace.Id, pbixPath, importName);

      Dataset dataset = GetDataset(pbiClient, workspace.Id, importName);

      UpdateMashupParametersRequest req = new UpdateMashupParametersRequest(new List<UpdateMashupParameterDetails>() {
        new UpdateMashupParameterDetails { Name = "DatabaseServer", NewValue = tenant.DatabaseServer },
        new UpdateMashupParameterDetails { Name = "DatabaseName", NewValue = tenant.DatabaseName }
      });

      pbiClient.Datasets.UpdateParametersInGroup(workspace.Id, dataset.Id, req);

      PatchSqlDatasourceCredentials(pbiClient, workspace.Id, dataset.Id, tenant.DatabaseUserName, tenant.DatabaseUserPassword);

      pbiClient.Datasets.RefreshDatasetInGroup(workspace.Id, dataset.Id);

      var profile = EnsureProfileExists(tenant.ProfileName);
      AddProfileAsWorkspaceMember(workspace.Id.ToString(), profile.id);

      return tenant;
    }

    public CustomerTenantDetails GetTenantDetails(CustomerTenant tenant) {

      SetCallingContext(tenant.ProfileName);

      return new CustomerTenantDetails {
        Name = tenant.Name,
        DatabaseName = tenant.DatabaseName,
        DatabaseServer = tenant.DatabaseServer,
        DatabaseUserName = tenant.DatabaseUserName,
        DatabaseUserPassword = tenant.DatabaseUserPassword,
        Profile = tenant.Profile,
        ProfileName = tenant.ProfileName,
        Created = tenant.Created,
        WorkspaceId = tenant.WorkspaceId,
        WorkspaceUrl = tenant.WorkspaceUrl,
        Members = GetWorkspaceMembers(tenant.WorkspaceId),
        Datasets = pbiClient.Datasets.GetDatasetsInGroup(new Guid(tenant.WorkspaceId)).Value,
        Reports = pbiClient.Reports.GetReportsInGroup(new Guid(tenant.WorkspaceId)).Value
      };

    }

    public CustomerTenant CreateAppWorkspace(PowerBIClient pbiClient, CustomerTenant tenant) {

      // create new app workspace
      GroupCreationRequest request = new GroupCreationRequest(tenant.Name);
      Group workspace = pbiClient.Groups.CreateGroup(request);

      // add user as new workspace admin to make demoing easier
      if (!string.IsNullOrEmpty(adminUser)) {
        pbiClient.Groups.AddGroupUser(workspace.Id, new GroupUser {
          EmailAddress = adminUser,
          GroupUserAccessRight = "Admin"
        });
      }

      tenant.WorkspaceId = workspace.Id.ToString();

      return tenant;
    }

    public void DeleteWorkspace(CustomerTenant tenant) {
      PowerBIClient pbiClient = this.GetPowerBiClient();
      Guid workspaceIdGuid = new Guid(tenant.WorkspaceId);
      pbiClient.Groups.DeleteGroup(workspaceIdGuid);
    }

    public void PublishPBIX(PowerBIClient pbiClient, Guid WorkspaceId, string PbixFilePath, string ImportName) {

      FileStream stream = new FileStream(PbixFilePath, FileMode.Open, FileAccess.Read);

      var import = pbiClient.Imports.PostImportWithFileInGroup(WorkspaceId, stream, ImportName);

      while (import.ImportState != "Succeeded") {
        import = pbiClient.Imports.GetImportInGroup(WorkspaceId, import.Id);
      }

    }

    public void PatchSqlDatasourceCredentials(PowerBIClient pbiClient, Guid WorkspaceId, string DatasetId, string SqlUserName, string SqlUserPassword) {

      var datasources = (pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId)).Value;

      // find the target SQL datasource
      foreach (var datasource in datasources) {
        if (datasource.DatasourceType.ToLower() == "sql") {
          // get the datasourceId and the gatewayId
          var datasourceId = datasource.DatasourceId;
          var gatewayId = datasource.GatewayId;
          // Create UpdateDatasourceRequest to update Azure SQL datasource credentials
          UpdateDatasourceRequest req = new UpdateDatasourceRequest {
            CredentialDetails = new CredentialDetails(
              new BasicCredentials(SqlUserName, SqlUserPassword),
              PrivacyLevel.None,
              EncryptedConnection.NotEncrypted)
          };
          // Execute Patch command to update Azure SQL datasource credentials
          pbiClient.Gateways.UpdateDatasource((Guid)gatewayId, (Guid)datasourceId, req);
        }
      };

    }

    public async Task<EmbeddedReportViewModel> GetReportEmbeddingData(string ProfileName, string Tenant) {

      
      var tenant = this.AppOwnsDataMultiTenantDbService.GetTenant(Tenant);
      Guid workspaceId = new Guid(tenant.WorkspaceId);

      SetCallingContext(tenant.ProfileName);

      var reports = (await pbiClient.Reports.GetReportsInGroupAsync(workspaceId)).Value;

      var report = reports.Where(report => report.Name.Equals("Sales")).First();

      GenerateTokenRequest generateTokenRequestParameters = new GenerateTokenRequest(accessLevel: "View");

      // call to Power BI Service API and pass GenerateTokenRequest object to generate embed token
      string embedToken = pbiClient.Reports.GenerateTokenInGroup(workspaceId, report.Id,
                                                                 generateTokenRequestParameters).Token;

      return new EmbeddedReportViewModel {
        ReportId = report.Id.ToString(),
        Name = report.Name,
        EmbedUrl = report.EmbedUrl,
        Token = embedToken,
        TenantName = Tenant
      };

    }

    public async Task<EmbeddedReportViewModel> GetEmbeddedViewModel(string Tenant) {

      var tenant = this.AppOwnsDataMultiTenantDbService.GetTenant(Tenant);
      Guid workspaceId = new Guid(tenant.WorkspaceId);

      SetCallingContext(tenant.ProfileName);


      var datasets = (await pbiClient.Datasets.GetDatasetsInGroupAsync(workspaceId)).Value;     
      var reports = (await pbiClient.Reports.GetReportsInGroupAsync(workspaceId)).Value;

      IList<GenerateTokenRequestV2Dataset> datasetRequests = new List<GenerateTokenRequestV2Dataset>();
      IList<string> datasetIds = new List<string>();

      foreach (var dataset in datasets) {
        datasetRequests.Add(new GenerateTokenRequestV2Dataset(dataset.Id));
        datasetIds.Add(dataset.Id);
      };
    
      IList<GenerateTokenRequestV2Report> reportRequests = new List<GenerateTokenRequestV2Report>();
      foreach (var report in reports) {
        reportRequests.Add(new GenerateTokenRequestV2Report(report.Id, allowEdit: false));
      };


      GenerateTokenRequestV2 tokenRequest =
        new GenerateTokenRequestV2 {
          Datasets = datasetRequests,
          Reports = reportRequests
        };

      // call to Power BI Service API and pass GenerateTokenRequest object to generate embed token
      var EmbedTokenResult = pbiClient.EmbedToken.GenerateToken(tokenRequest);

      Report targetreport = reports.Where(report => report.Name.Equals("Sales")).First();

      return new EmbeddedReportViewModel {
        ReportId = targetreport.Id.ToString(),
        Name = targetreport.Name,
        EmbedUrl = targetreport.EmbedUrl,
        Token = EmbedTokenResult.Token,
        TenantName = Tenant
      };

    }

  }

}
