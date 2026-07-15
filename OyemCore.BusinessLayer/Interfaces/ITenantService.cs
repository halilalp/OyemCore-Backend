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

        // True when the current tenant's StorageFolder is a URL (e.g. "https://oyemsoft.com/")
        // rather than a filesystem path. Callers that need to write files locally must check this
        // first — there is no generic way to write bytes to an arbitrary URL.
        bool IsStorageRemote();

        // Resolves the current tenant's StorageFolder to an absolute local filesystem path,
        // applying the same wwwroot-fallback / relative-path-rooting rules everywhere.
        // Do not call when IsStorageRemote() is true — the result would be meaningless.
        string ResolveLocalStorageFolder(string contentRootPath);

        // Uploads a file to the tenant's remote storage (its webportal, when IsStorageRemote() is
        // true) via that webportal's WebServiceFileUpload.asmx. Only call when IsStorageRemote().
        System.Threading.Tasks.Task<(bool Success, string RelativePath, string Error)> UploadToRemoteStorageAsync(string relativePath, string fileBase64);
    }
}
