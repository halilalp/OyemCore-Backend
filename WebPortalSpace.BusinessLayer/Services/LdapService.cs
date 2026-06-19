using System;
using System.DirectoryServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebPortalSpace.BusinessLayer.Interfaces;

namespace WebPortalSpace.BusinessLayer.Services
{
    public class LdapService : ILdapService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LdapService> _logger;

        public LdapService(IConfiguration configuration, ILogger<LdapService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public (bool Success, string Email, string ErrorMessage) ValidateUser(string username, string password)
        {
            try
            {
                var ldapServer = _configuration["Ldap:Server"] ?? "192.168.2.236";
                var domain = _configuration["Ldap:Domain"] ?? "isiktarim";

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
                                email = de.Properties["mail"].Value.ToString().ToLower().Replace("ı", "i");
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
