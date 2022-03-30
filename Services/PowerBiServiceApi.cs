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

  public class PowerBiServiceApi {

    private string adminUser { get; }
    private string servicePrincipalObjectId { get; }
    private string capacityId { get; }
    private ITokenAcquisition tokenAcquisition { get; }
    private readonly IWebHostEnvironment environment;
    private PowerBIClient pbiClient;

    private const string powerbiApiDefaultScope = "https://analysis.windows.net/powerbi/api/.default";
    private const string urlPowerBiServiceApiRoot = "https://api.powerbi.com/";

    public PowerBiServiceApi(IConfiguration configuration, IWebHostEnvironment environment,
                             ITokenAcquisition tokenAcquisition) {
      this.adminUser = configuration["DemoSettings:AdminUser"];
      this.servicePrincipalObjectId = configuration["DemoSettings:ServicePrincipalObjectId"];
      this.capacityId = configuration["DemoSettings:CapacityId"];
      this.environment = environment;
      this.tokenAcquisition = tokenAcquisition;
      this.pbiClient = GetPowerBiClient();
    }

    private string GetAccessToken() {
      return this.tokenAcquisition.GetAccessTokenForAppAsync(powerbiApiDefaultScope).Result;
    }

    private PowerBIClient GetPowerBiClient() {
      var uriPowerBiServiceApiRoot = new Uri(urlPowerBiServiceApiRoot);
      var tokenCredentials = new TokenCredentials(GetAccessToken(), "Bearer");

      // create PowerBIClient for service principal
      return new PowerBIClient(uriPowerBiServiceApiRoot, tokenCredentials);
    }

    private PowerBIClient GetPowerBiClientForProfile(Guid ProfileId) {
      var uriPowerBiServiceApiRoot = new Uri(urlPowerBiServiceApiRoot);
      var tokenCredentials = new TokenCredentials(GetAccessToken(), "Bearer");
      
      // create PowerBIClient for service principal profile
      return new PowerBIClient(uriPowerBiServiceApiRoot, tokenCredentials, ProfileId);
    }

    private void GetPowerBiClientForProfileAlt(string ProfileId) {

      // create PowerBIClient 
      var uriPowerBiServiceApiRoot = new Uri(urlPowerBiServiceApiRoot);
      var tokenCredentials = new TokenCredentials(GetAccessToken(), "Bearer");
      var pbiClient = new PowerBIClient(uriPowerBiServiceApiRoot, tokenCredentials);

      // add X-PowerBI-profile-id header for service principal profile
      pbiClient.HttpClient.DefaultRequestHeaders.Add("X-PowerBI-profile-id", ProfileId);

      // execute call under identity of service principal profile
      var workspaces = pbiClient.Groups.GetGroups();

    }

    private void SetCallingContext(string ProfileId = "") {

      if (ProfileId.Equals("")) {
        pbiClient = GetPowerBiClient();
      }
      else {
        pbiClient = GetPowerBiClientForProfile(new Guid(ProfileId));
      }

    }

    private Import PublishPBIX(PowerBIClient pbiClient, Guid WorkspaceId, string PbixFilePath, string ImportName) {
      FileStream stream = new FileStream(PbixFilePath, FileMode.Open, FileAccess.Read);
      var import = pbiClient.Imports.PostImportWithFileInGroup(WorkspaceId, stream, ImportName);
      while (import.ImportState != "Succeeded") {
        System.Threading.Thread.Sleep(1000);
        import = pbiClient.Imports.GetImportInGroup(WorkspaceId, import.Id);
      }
      return import;
    }

    private void PatchSqlDatasourceCredentials(PowerBIClient pbiClient, Guid WorkspaceId, string DatasetId, string SqlUserName, string SqlUserPassword) {

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

    public IList<ServicePrincipalProfile> GetProfiles() {
      SetCallingContext();
      var profiles = pbiClient.Profiles.GetProfiles().Value;
      return profiles;
    }

    public ServicePrincipalProfile GetProfile(string ProfileName) {
      SetCallingContext();
      var profiles = GetProfiles();
      foreach (var profile in profiles) {
        if (profile.DisplayName.Equals(ProfileName)) {
          return profile;
        }
      }
      return null;
    }

    public ServicePrincipalProfile CreateProfile(string ProfileName) {

      // always create & manage profiles as service principal
      SetCallingContext();       

      var createRequest = new CreateOrUpdateProfileRequest(ProfileName);
      var profile = pbiClient.Profiles.CreateProfile(createRequest);      
      return profile;
    }

    public void DeleteProfile(string ProfileId) {

      // always create & manage profiles as service principal
      SetCallingContext();

      pbiClient.Profiles.DeleteProfile(new Guid(ProfileId));
    }

    public async Task<IList<Group>> GetTenantWorkspaces(PowerBIClient pbiClient) {
      var workspaces = (await pbiClient.Groups.GetGroupsAsync()).Value;
      return workspaces;
    }

    public CustomerTenant OnboardNewTenant(CustomerTenant tenant) {

      // execute call under identity of serivce principal profile
      SetCallingContext(tenant.Profile.ProfileId);
  
      // create new app workspace
      GroupCreationRequest request = new GroupCreationRequest(tenant.Name);
      Group workspace = pbiClient.Groups.CreateGroup(request);

      tenant.WorkspaceId = workspace.Id.ToString();
      tenant.WorkspaceUrl = "https://app.powerbi.com/groups/" + workspace.Id.ToString() + "/";

      // associate workspace with a premium/embedded capacity
      if (!string.IsNullOrEmpty(capacityId)) {
        var assignReqest = new AssignToCapacityRequest(new Guid(capacityId));
        pbiClient.Groups.AssignToCapacity(workspace.Id, assignReqest);
      }

      // add user as new workspace admin to make demoing easier
      if (!string.IsNullOrEmpty(adminUser)) {
        pbiClient.Groups.AddGroupUser(workspace.Id, new GroupUser {
          Identifier = adminUser,
          PrincipalType = PrincipalType.User,
          EmailAddress= adminUser,
          GroupUserAccessRight = "Admin"
        });
      }

      // add service principal as workspace
      if (!string.IsNullOrEmpty(servicePrincipalObjectId)) {
        var newMember = new GroupUser {
          Identifier = servicePrincipalObjectId,
          PrincipalType = PrincipalType.App,
          GroupUserAccessRight = "Admin"
        };
        // uncomment the next line to add service principal to new workspace
        // pbiClient.Groups.AddGroupUser(workspace.Id, newMember);
      }

      // upload sample PBIX template file to create dataset and report
      string pbixPath = this.environment.WebRootPath + @"/PBIX/DatasetTemplate.pbix";
      string importName = "Sales";
      var import = PublishPBIX(pbiClient, workspace.Id, pbixPath, importName);

      var datasetId = import.Datasets[0].Id;

      UpdateMashupParametersRequest updateRequest = new UpdateMashupParametersRequest(new List<UpdateMashupParameterDetails>() {
        new UpdateMashupParameterDetails { Name = "DatabaseServer", NewValue = tenant.DatabaseServer },
        new UpdateMashupParameterDetails { Name = "DatabaseName", NewValue = tenant.DatabaseName }
      });

      pbiClient.Datasets.UpdateParametersInGroup(workspace.Id, datasetId, updateRequest);

      PatchSqlDatasourceCredentials(pbiClient, workspace.Id, datasetId, tenant.DatabaseUserName, tenant.DatabaseUserPassword);

      pbiClient.Datasets.RefreshDatasetInGroup(workspace.Id, datasetId);    

      return tenant;
    }

    public CustomerTenantDetails GetTenantDetails(CustomerTenant tenant) {

      // execute call under identity of serivce principal profile
      SetCallingContext(tenant.Profile.ProfileId);

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
        Members = pbiClient.Groups.GetGroupUsers(new Guid(tenant.WorkspaceId)).Value,
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
      SetCallingContext(tenant.Profile.ProfileId);
      pbiClient.Groups.DeleteGroup(new Guid(tenant.WorkspaceId));
    }

    public async Task<EmbeddedReportViewModel> GetReportEmbeddingData(CustomerTenant tenant) {

      // execute call under identity of serivce principal profile
      SetCallingContext(tenant.Profile.ProfileId);

      Guid workspaceId = new Guid(tenant.WorkspaceId); 
      var reports = (await pbiClient.Reports.GetReportsInGroupAsync(workspaceId)).Value;
      var report = reports.Where(report => report.Name.Equals("Sales")).First();

      var datasetRequest = new GenerateTokenRequestV2Dataset { Id = report.DatasetId };
      var reportRequest = new GenerateTokenRequestV2Report { Id = report.Id };

      var tokenRequest = new GenerateTokenRequestV2 {
        Datasets = new List<GenerateTokenRequestV2Dataset>() { datasetRequest },
        Reports = new List<GenerateTokenRequestV2Report>() { reportRequest }
      };

      // call to Power BI Service API to generate embed token as serivce principal profile
      string embedToken = pbiClient.EmbedToken.GenerateToken(tokenRequest).Token;

      return new EmbeddedReportViewModel {
        ReportId = report.Id.ToString(),
        Name = report.Name,
        EmbedUrl = report.EmbedUrl,
        Token = embedToken,
        TenantName = tenant.Name
      };

    }

    public async Task<EmbeddedReportViewModel> GetEmbeddedViewModel(CustomerTenant tenant) {

      SetCallingContext(tenant.Profile.ProfileId);

      Guid workspaceId = new Guid(tenant.WorkspaceId);
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
        TenantName = tenant.Name
      };

    }

    public void AddProfileAsWorkspaceMember(Guid WorkspaceId, string ServicePrincipalId, Guid ProfileId) {

      var groupUser = new GroupUser {
        GroupUserAccessRight = "Admin",
        PrincipalType = "App",
        Identifier = ServicePrincipalId,
        Profile = new ServicePrincipalProfile {
          Id = ProfileId
        }
      };

      pbiClient.Groups.AddGroupUser(WorkspaceId, groupUser);

    }

  }

}
