namespace OyemCore.BusinessLayer.Interfaces
{
    public interface ITenantService
    {
        string GetCurrentConnectionString();
        string GetCurrentMailConnectionString();
        string GetCurrentMeetingConnectionString();
        string GetCurrentLdapServer();
        string GetCurrentLdapDomain();
        string GetCurrentStorageFolder();
        string GetModulPath(string modul);
    }
}
