namespace OyemCore.BusinessLayer.Interfaces
{
    public interface ITenantService
    {
        // KOK NEDEN (2026-09-23): ChatHub.Users (aktif SignalR baglantilarini SicilNo'ya gore tutan
        // static sozluk) tenant'tan bagimsizdi — api.oyemsoft.com host'unu oyemsoft/adore/ashley AYNI
        // ANDA paylastigindan, iki farkli sirkette AYNI SicilNo (or. "SCL0001-00") varsa aramalar/
        // sohbet bildirimleri birbirine KARISIYORDU (canlida dogrulandi: oyemsoft kullanicisina
        // gelen bir arama, baska tenant'ta ayni SicilNo ile oturum acan kullaniciya gorunmustu).
        // Bu metod, ChatHub'in (ve IChatRealtimeDispatcher'in) sunucu-tarafi presence sozluklerini
        // "tenantId|sicilNo" seklinde tenant'a gore ayirabilmesi icin GetCurrentTenant()'in TenantId'sini
        // disariya acar.
        string GetCurrentTenantId();

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

        // Current tenant's WebPortal base URL, when known — reuses StorageFolder (it IS the
        // webportal's own URL for tenants configured with remote storage, e.g. "https://oyemsoft.com/").
        // Null when the tenant has no remote WebPortal (e.g. local-storage-only tenants) — callers
        // must treat that as "skip, not an error".
        string GetWebPortalBaseUrl();
    }
}
