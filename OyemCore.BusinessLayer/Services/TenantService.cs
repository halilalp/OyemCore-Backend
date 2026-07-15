using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using OyemCore.DataLayer.Contexts;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.BusinessLayer.Common;

namespace OyemCore.BusinessLayer.Services
{
    public class TenantService : ITenantService
    {
        private readonly MasterDbContext _masterDbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public TenantService(MasterDbContext masterDbContext, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _masterDbContext = masterDbContext;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        private OyemCore.DataLayer.Entities.Tenant GetCurrentTenant()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            string tenantId = null;

            // 1. Try to get from JWT Claims (for authenticated requests)
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                var tenantClaim = httpContext.User.FindFirst("TenantId") ?? httpContext.User.FindFirst("SirketKodu");
                if (tenantClaim != null)
                {
                    tenantId = tenantClaim.Value;
                }
            }

            // 2. Try to get from Header (for login or other requests)
            if (string.IsNullOrEmpty(tenantId) && httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var headerTenantId))
            {
                tenantId = headerTenantId.ToString();
            }

            // 2.5 Try to get from Query string (for image downloads where headers/JWT are not present)
            if (string.IsNullOrEmpty(tenantId) && httpContext.Request.Query.TryGetValue("tenantId", out var queryTenantId))
            {
                tenantId = queryTenantId.ToString();
            }

            // 3. Try to get by Request Host (for third-party integrations, webhooks, or web apps hitting domain e.g. api.oyemsoft.com)
            if (string.IsNullOrEmpty(tenantId))
            {
                var host = httpContext.Request.Host.Host;
                if (!string.IsNullOrEmpty(host) && host != "localhost" && host != "127.0.0.1" && host != "10.0.2.2")
                {
                    var tenantByHost = _masterDbContext.Tenants.FirstOrDefault(t => t.ApiServer == host && t.IsActive);
                    if (tenantByHost != null)
                    {
                        tenantId = tenantByHost.TenantId;
                    }
                }
            }

            if (string.IsNullOrEmpty(tenantId))
            {
                return null;
            }

            return _masterDbContext.Tenants.FirstOrDefault(t => t.TenantId == tenantId && t.IsActive);
        }

        public string GetCurrentConnectionString()
        {
            // 1. Prioritize local configuration from appsettings.json if defined
            var localConnString = _configuration.GetConnectionString("YbsDB");
            if (!string.IsNullOrEmpty(localConnString))
            {
                return localConnString;
            }

            // 2. Fallback to dynamic tenant resolution from MasterDB
            var tenant = GetCurrentTenant();
            if (tenant == null || string.IsNullOrEmpty(tenant.ConnectionString))
            {
                return null;
            }

            string encryptionKey = _configuration["Encryption:TenantKey"];
            return SecurityHelper.DecryptString(tenant.ConnectionString, encryptionKey);
        }

        public string GetCurrentMailConnectionString()
        {
            // 1. Prioritize local configuration
            var localMailConn = _configuration.GetConnectionString("MailDB");
            if (!string.IsNullOrEmpty(localMailConn))
            {
                return localMailConn;
            }

            // 2. Fallback
            var tenant = GetCurrentTenant();
            if (tenant == null || string.IsNullOrEmpty(tenant.MailConnectionString))
            {
                return null;
            }

            string encryptionKey = _configuration["Encryption:TenantKey"];
            return SecurityHelper.DecryptString(tenant.MailConnectionString, encryptionKey);
        }

        public string GetCurrentMeetingConnectionString()
        {
            // 1. Prioritize local configuration
            var localMeetingConn = _configuration.GetConnectionString("MeetingDB");
            if (!string.IsNullOrEmpty(localMeetingConn))
            {
                return localMeetingConn;
            }

            // 2. Fallback
            var tenant = GetCurrentTenant();
            if (tenant == null || string.IsNullOrEmpty(tenant.MeetingConnectionString))
            {
                return null;
            }

            string encryptionKey = _configuration["Encryption:TenantKey"];
            return SecurityHelper.DecryptString(tenant.MeetingConnectionString, encryptionKey);
        }

        public string GetCurrentLdapServer()
        {
            // 1. Prioritize local configuration from appsettings.json if defined
            var localLdap = _configuration["Ldap:Server"];
            if (!string.IsNullOrEmpty(localLdap))
            {
                return localLdap;
            }

            // 2. Fallback to dynamic tenant resolution
            var tenant = GetCurrentTenant();
            if (tenant == null || string.IsNullOrEmpty(tenant.LdapServer))
            {
                return null;
            }

            string encryptionKey = _configuration["Encryption:TenantKey"];
            return SecurityHelper.DecryptString(tenant.LdapServer, encryptionKey);
        }

        public string GetCurrentLdapDomain()
        {
            // 1. Prioritize local configuration
            var localDomain = _configuration["Ldap:Domain"];
            if (!string.IsNullOrEmpty(localDomain))
            {
                return localDomain;
            }

            // 2. Fallback
            var tenant = GetCurrentTenant();
            return tenant?.LdapDomain;
        }

        // Config-routing signal only — NOT a trust/authorization boundary. A spoofed value only
        // changes which config source GetCurrentStorageFolder()/GetModulPath() consult; it never
        // changes which tenant's data is exposed (that is governed by GetCurrentTenant() above).
        private bool IsMobileClient()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return false;

            if (httpContext.Request.Headers.TryGetValue("X-Client-Type", out var headerValue) &&
                string.Equals(headerValue.ToString(), "mobile", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Fallback for requests that never go through the shared axios client (e.g. Files/download
            // hit directly via Linking.openURL or an <Image> uri), mirroring the tenantId header->query fallback above.
            if (httpContext.Request.Query.TryGetValue("clientType", out var queryValue) &&
                string.Equals(queryValue.ToString(), "mobile", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public string GetCurrentStorageFolder()
        {
            // 1. Try to get from Tenant DB first (if tenant is resolved)
            var tenant = GetCurrentTenant();
            if (tenant != null && !string.IsNullOrEmpty(tenant.StorageFolder))
            {
                return tenant.StorageFolder;
            }

            // 2. Fallback to local configuration — appsettings.json is for non-mobile (web/3rd-party)
            // callers only. Mobile requests must resolve strictly from the Tenant record in MasterDB.
            if (!IsMobileClient())
            {
                var localStorage = _configuration["Storage:Folder"];
                if (!string.IsNullOrEmpty(localStorage))
                {
                    return localStorage;
                }
            }

            return null;
        }

        public bool IsStorageRemote()
        {
            var storageFolder = GetCurrentStorageFolder() ?? "";
            return storageFolder.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase)
                || storageFolder.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase);
        }

        public string ResolveLocalStorageFolder(string contentRootPath)
        {
            string storageFolder = GetCurrentStorageFolder() ?? "";
            if (string.IsNullOrEmpty(storageFolder))
            {
                return System.IO.Path.Combine(contentRootPath, "wwwroot");
            }
            if (!System.IO.Path.IsPathRooted(storageFolder))
            {
                return System.IO.Path.GetFullPath(System.IO.Path.Combine(contentRootPath, storageFolder));
            }
            return storageFolder;
        }

        public async Task<(bool Success, string RelativePath, string Error)> UploadToRemoteStorageAsync(string relativePath, string fileBase64)
        {
            var storageFolder = GetCurrentStorageFolder() ?? "";
            if (string.IsNullOrEmpty(storageFolder))
            {
                return (false, null, "Tenant için StorageFolder tanımlı değil.");
            }

            var apiKey = _configuration["Internal:FileUploadApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                return (false, null, "FileUploadApiKey yapılandırılmamış (appsettings.json > Internal:FileUploadApiKey).");
            }

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

                var url = $"{storageFolder.TrimEnd('/')}/WebServiceFileUpload.asmx/DosyaYukle";
                var formData = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["apiKey"] = apiKey,
                    ["relativePath"] = relativePath,
                    ["fileBase64"] = fileBase64
                });

                var response = await client.PostAsync(url, formData);
                var raw = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (false, null, $"Webportal HTTP {(int)response.StatusCode}: {raw}");
                }

                // ASMX HttpPost protokolü, dönen string'i bir XML <string> elemanına sarar.
                string jsonText = raw;
                try
                {
                    var doc = System.Xml.Linq.XDocument.Parse(raw);
                    jsonText = doc.Root?.Value ?? raw;
                }
                catch
                {
                    // Zaten düz JSON gelmişse (SOAP sarmalama yoksa) olduğu gibi kullan.
                }

                using var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonText);
                var root = jsonDoc.RootElement;
                bool success = root.TryGetProperty("success", out var successEl) && successEl.GetBoolean();
                string returnedPath = root.TryGetProperty("relativePath", out var pathEl) ? pathEl.GetString() : relativePath;
                string message = root.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null;

                return success ? (true, returnedPath, null) : (false, null, message ?? "Webportal yükleme başarısız.");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        public string GetModulPath(string modul)
        {
            if (string.IsNullOrEmpty(modul))
            {
                return "HelpDesk/Docs";
            }

            modul = modul.ToUpper();

            // 1. Check tenant ModulPaths from DB first
            var tenant = GetCurrentTenant();
            if (tenant != null && !string.IsNullOrEmpty(tenant.ModulPaths))
            {
                try
                {
                    var modulPathsDict = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(tenant.ModulPaths);
                    if (modulPathsDict != null && modulPathsDict.TryGetValue(modul, out var tenantPath))
                    {
                        return tenantPath;
                    }
                }
                catch
                {
                    // Ignore JSON parsing errors
                }
            }

            // 2. Check local configuration second — appsettings.json is for non-mobile callers only.
            // Mobile requests skip straight to the hardcoded defaults below (not configuration).
            if (!IsMobileClient())
            {
                var localPath = _configuration[$"Storage:Modules:{modul}"];
                if (!string.IsNullOrEmpty(localPath))
                {
                    return localPath;
                }
            }

            // 3. Fallback to hardcoded defaults
            switch (modul)
            {
                case "AVATAR":
                    return "theme/src/media/avatars";
                case "CALENDAR":
                    return "Dashboard/json";
                case "TICKET":
                    return "Ticket/Docs";
                case "PROJE":
                    return "Proje/Docs";
                case "TALEP":
                case "HELPDESK":
                case "IT":
                case "ERP":
                    return "HelpDesk/Docs";
                case "ISG":
                    return "Isg/Docs";
                case "BAKIM":
                    return "Bakim/Docs";
                case "SANTIYE":
                    return "Santiye/Docs";
                case "YAZILIM":
                    return "Yazilim/Docs";
                case "ARGE":
                    return "Arge/Docs";
                case "DASHBOARD":
                    return "Dashboard/json";
                case "HABERDOCS":
                    return "DataYonetim/Docs";
                case "HABERIMG":
                    return "DataYonetim/img";
                default:
                    return "HelpDesk/Docs";
            }
        }
    }
}
