using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using OyemCore.BusinessLayer.Interfaces;
using System.IO;

namespace OyemCore.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private readonly ITenantService _tenantService;
        private readonly IContentTypeProvider _contentTypeProvider;
        private readonly IWebHostEnvironment _env;

        public FilesController(ITenantService tenantService, IWebHostEnvironment env)
        {
            _tenantService = tenantService;
            _env = env;
            _contentTypeProvider = new FileExtensionContentTypeProvider();
        }

        [HttpGet("download")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public IActionResult Download([FromQuery] string? relativePath, [FromQuery] string? module, [FromQuery] string? fileName, [FromQuery] string? tenantId, [FromQuery] bool inline = false)
        {
            try
            {
                // StorageFolder bir URL de olabilir (örn. "https://oyemsoft.com/"). Bu durumda dosya
                // yerelde aranmaz; StorageFolder + ModulPath (+ fileName) birleştirilip o adrese yönlendirilir.
                if (_tenantService.IsStorageRemote())
                {
                    string remoteBase = _tenantService.GetCurrentStorageFolder() ?? "";
                    string subPath;
                    if (!string.IsNullOrEmpty(relativePath))
                    {
                        subPath = relativePath.Replace("..", "").TrimStart('/', '\\').Replace('\\', '/');
                    }
                    else if (!string.IsNullOrEmpty(module) && !string.IsNullOrEmpty(fileName))
                    {
                        string modulePath = _tenantService.GetModulPath(module).Replace('\\', '/').Trim('/');
                        subPath = $"{modulePath}/{fileName}";
                    }
                    else
                    {
                        return BadRequest("relativePath veya module + fileName parametreleri gereklidir.");
                    }

                    return Redirect($"{remoteBase.TrimEnd('/')}/{subPath}");
                }

                string storageFolder = _tenantService.ResolveLocalStorageFolder(_env.ContentRootPath);

                string fullPath = "";

                if (!string.IsNullOrEmpty(relativePath))
                {
                    // Frontend 'HelpDesk/Docs/dosya.jpg' gönderiyor. StorageFolder ile birleştirilir.
                    relativePath = relativePath.Replace("..", "").TrimStart('/', '\\');
                    fullPath = Path.Combine(storageFolder, relativePath);
                }
                else if (!string.IsNullOrEmpty(module) && !string.IsNullOrEmpty(fileName))
                {
                    // ModulPath MasterDB'den (veya TenantService üzerinden) çözülür
                    string modulePath = _tenantService.GetModulPath(module);
                    fullPath = Path.Combine(storageFolder, modulePath, fileName);
                }
                else
                {
                    return BadRequest("relativePath veya module + fileName parametreleri gereklidir.");
                }

                // Security check
                if (!Path.GetFullPath(fullPath).StartsWith(Path.GetFullPath(storageFolder), System.StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("Geçersiz dosya yolu erişimi.");
                }

                if (!System.IO.File.Exists(fullPath))
                {
                    return NotFound(new { message = $"Dosya sunucuda bulunamadı: {fullPath}" });
                }

                new FileExtensionContentTypeProvider().TryGetContentType(fullPath, out string contentType);
                contentType ??= "application/octet-stream";

                if (inline)
                {
                    // PhysicalFile automatically sets correct headers for inline rendering and cache-validation.
                    // This is essential for React Native <Image> component.
                    return PhysicalFile(fullPath, contentType);
                }
                else
                {
                    var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return File(fileStream, contentType, Path.GetFileName(fullPath));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Hata oluştu.", error = ex.Message });
            }
        }
    }
}
