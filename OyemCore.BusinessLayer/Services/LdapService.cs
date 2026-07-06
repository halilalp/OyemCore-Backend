using System;
using System.DirectoryServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    public class LdapService : ILdapService
    {
        private readonly ITenantService _tenantService;
        private readonly ILogger<LdapService> _logger;

        public LdapService(ITenantService tenantService, ILogger<LdapService> logger)
        {
            _tenantService = tenantService;
            _logger = logger;
        }

        public (bool Success, string Email, string ErrorMessage) ValidateUser(string username, string password)
        {
            try
            {
                var ldapServer = _tenantService.GetCurrentLdapServer() ?? "192.168.2.236";
                var domain = _tenantService.GetCurrentLdapDomain() ?? "isiktarim";

                _logger.LogInformation($"Attempting LDAP login for user {username} on server {ldapServer}");

                string adUser = $"{domain}\\{username}";

                using (DirectoryEntry ldapConnection = new DirectoryEntry($"LDAP://{ldapServer}", adUser, password, AuthenticationTypes.Secure))
                {
                    object nativeObject = ldapConnection.NativeObject;

                    using (DirectorySearcher deSearch = new DirectorySearcher())
                    {
                        deSearch.SearchRoot = ldapConnection;
                        deSearch.Filter = $"sAMAccountName={username}";
                        SearchResult result = deSearch.FindOne();

                        if (result != null)
                        {
                            DirectoryEntry de = result.GetDirectoryEntry();
                            string email = "";

                            if (de.Properties.Contains("mail") && de.Properties["mail"].Value != null)
                            {
                                email = de.Properties["mail"].Value.ToString().ToLower().Replace("i", "i");
                            }
                            else
                            {
                                email = $"{username.ToLower()}@isiktarim.com";
                            }

                            return (true, email, null);
                        }
                    }
                }

                return (false, null, "User entry not found in Active Directory.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"LDAP login failed for user {username}");
                return (false, null, ex.Message);
            }
        }
    }
}
