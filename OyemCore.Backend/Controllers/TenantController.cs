using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OyemCore.BusinessLayer.Common;
using OyemCore.DataLayer.Contexts;
using OyemCore.DataLayer.Entities;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.Backend.Controllers
{
    // referans: WebPortal Admin/OyemSoftTenantIslemleri.html — OyemSoft'un kendi platform
    // genelindeki tüm müşteri tenant kayıtlarını (ham DB connection string'leri dahil) yönetir.
    // Bu yüzden sadece "oyemsoft" tenant'ıyla giriş yapan kullanıcılara açık (kullanıcı kararı) —
    // JWT "TenantId" claim'i (login sırasında seçilen şirket) master DB'deki Tenant.TenantId ile
    // aynı değer, WebPortal'daki gibi ayrı bir SirketKodu/yetki tablosu değil.
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TenantController : ControllerBase
    {
        private const string OyemsoftTenantId = "oyemsoft";

        private readonly MasterDbContext _masterDb;
        private readonly IYbsDbContext _context;
        private readonly IConfiguration _configuration;

        public TenantController(MasterDbContext masterDb, IYbsDbContext context, IConfiguration configuration)
        {
            _masterDb = masterDb;
            _context = context;
            _configuration = configuration;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int id))
            {
                return id;
            }
            throw new UnauthorizedAccessException("Giris yapan kullanici kimligi dogrulanamadi.");
        }

        private bool IsOyemsoftTenant()
        {
            var claim = User.FindFirst("TenantId");
            return claim != null && string.Equals(claim.Value, OyemsoftTenantId, StringComparison.OrdinalIgnoreCase);
        }

        // Forbid() alone returns an empty 403 body, which the mobile client can't show anything useful
        // for — this includes the actual claim value seen so a tenant mismatch is diagnosable from the UI.
        private IActionResult ForbidWithReason()
        {
            var claim = User.FindFirst("TenantId");
            return StatusCode(403, new { message = $"Bu sayfa sadece oyemsoft tenant'ıyla girişte kullanılabilir (görülen TenantId: '{claim?.Value ?? "(yok)"}')." });
        }

        private string TenantKey => _configuration["Encryption:TenantKey"];

        private string Decrypt(string cipherText) => SecurityHelper.DecryptString(cipherText, TenantKey);
        private string Encrypt(string plainText) => SecurityHelper.EncryptString(plainText, TenantKey);

        [HttpGet]
        public IActionResult GetTenants([FromQuery] string search)
        {
            if (!IsOyemsoftTenant()) return ForbidWithReason();

            try
            {
                var query = _masterDb.Tenants.AsQueryable();
                if (!string.IsNullOrEmpty(search))
                {
                    var s = search.ToLower();
                    query = query.Where(t => t.TenantId.ToLower().Contains(s) || t.Unvan.ToLower().Contains(s));
                }

                var list = query.OrderBy(t => t.Unvan).ToList().Select(t => new
                {
                    t.TenantId,
                    Unvan = t.Unvan ?? "",
                    ConnectionString = Decrypt(t.ConnectionString),
                    MailConnectionString = Decrypt(t.MailConnectionString),
                    MeetingConnectionString = Decrypt(t.MeetingConnectionString),
                    StorageFolder = t.StorageFolder ?? "",
                    t.IsActive,
                    LdapServer = Decrypt(t.LdapServer),
                    LdapDomain = t.LdapDomain ?? "",
                    ModulPaths = t.ModulPaths ?? "",
                    IsMailService = t.IsMailService ?? false,
                    IsSmsService = t.IsSmsService ?? false,
                    ApiServer = t.ApiServer ?? ""
                }).ToList();

                return Ok(new { success = true, data = list });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult SaveTenant([FromBody] TenantSaveDto dto)
        {
            if (!IsOyemsoftTenant()) return ForbidWithReason();

            if (dto == null || string.IsNullOrWhiteSpace(dto.TenantId))
                return BadRequest(new { success = false, message = "Tenant ID boş olamaz." });
            if (string.IsNullOrWhiteSpace(dto.Unvan))
                return BadRequest(new { success = false, message = "Şirket Ünvanı boş olamaz." });
            if (dto.IsNew && string.IsNullOrWhiteSpace(dto.ConnectionString))
                return BadRequest(new { success = false, message = "Connection String boş olamaz." });

            try
            {
                Tenant t;
                if (dto.IsNew)
                {
                    if (_masterDb.Tenants.Any(o => o.TenantId.ToLower() == dto.TenantId.Trim().ToLower()))
                        return BadRequest(new { success = false, message = $"Bu Tenant ID ({dto.TenantId}) zaten sistemde kayıtlı." });

                    t = new Tenant { TenantId = dto.TenantId.Trim() };
                    _masterDb.Tenants.Add(t);
                }
                else
                {
                    t = _masterDb.Tenants.FirstOrDefault(o => o.TenantId == dto.TenantId);
                    if (t == null)
                        return NotFound(new { success = false, message = "Güncellenecek tenant kaydı bulunamadı." });
                }

                t.Unvan = dto.Unvan.Trim();
                if (!string.IsNullOrEmpty(dto.ConnectionString)) t.ConnectionString = Encrypt(dto.ConnectionString);
                t.MailConnectionString = string.IsNullOrEmpty(dto.MailConnectionString) ? null : Encrypt(dto.MailConnectionString);
                t.MeetingConnectionString = string.IsNullOrEmpty(dto.MeetingConnectionString) ? null : Encrypt(dto.MeetingConnectionString);
                t.StorageFolder = dto.StorageFolder?.Trim() ?? "";
                t.IsActive = dto.IsActive;
                t.LdapServer = string.IsNullOrEmpty(dto.LdapServer) ? null : Encrypt(dto.LdapServer);
                t.LdapDomain = dto.LdapDomain?.Trim() ?? "";
                t.ModulPaths = dto.ModulPaths?.Trim() ?? "";
                t.IsMailService = dto.IsMailService;
                t.IsSmsService = dto.IsSmsService;
                t.ApiServer = dto.ApiServer?.Trim() ?? "";

                _masterDb.SaveChanges();

                LogAction("TENANT_KAYDET", $"Tenant kaydı {(dto.IsNew ? "eklendi" : "güncellendi")} ({dto.Unvan}). TenantId: {dto.TenantId}");

                return Ok(new { success = true, message = dto.IsNew ? "Tenant başarıyla eklendi." : "Tenant başarıyla güncellendi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpDelete("{tenantId}")]
        public IActionResult DeleteTenant(string tenantId)
        {
            if (!IsOyemsoftTenant()) return ForbidWithReason();
            if (string.IsNullOrWhiteSpace(tenantId))
                return BadRequest(new { success = false, message = "Tenant ID boş olamaz." });

            try
            {
                var t = _masterDb.Tenants.FirstOrDefault(o => o.TenantId == tenantId);
                if (t == null)
                    return NotFound(new { success = false, message = "Silinecek tenant kaydı bulunamadı." });

                var unvan = t.Unvan;
                _masterDb.Tenants.Remove(t);
                _masterDb.SaveChanges();

                LogAction("TENANT_SIL", $"Tenant kaydı silindi ({unvan}). TenantId: {tenantId}");

                return Ok(new { success = true, message = "Tenant başarıyla silindi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        private void LogAction(string konu, string aciklama)
        {
            try
            {
                var kullaniciId = GetCurrentUserId();
                var sicilNo = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciId)?.SicilNo ?? "";
                var eposta = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciId)?.Eposta ?? "";

                _context.tb_Log.Add(new tb_Log
                {
                    SicilNo = sicilNo,
                    Eposta = eposta,
                    Konu = konu,
                    Aciklama = aciklama,
                    KayitTar = DateTime.Now,
                    Cihaz = "mobil"
                });
                _context.SaveChanges();
            }
            catch { /* log hatası ana işlemi engellemez */ }
        }
    }

    public class TenantSaveDto
    {
        public string TenantId { get; set; }
        public string Unvan { get; set; }
        public string ConnectionString { get; set; }
        public string MailConnectionString { get; set; }
        public string MeetingConnectionString { get; set; }
        public string StorageFolder { get; set; }
        public bool IsActive { get; set; }
        public string LdapServer { get; set; }
        public string LdapDomain { get; set; }
        public string ModulPaths { get; set; }
        public bool IsMailService { get; set; }
        public bool IsSmsService { get; set; }
        public string ApiServer { get; set; }
        public bool IsNew { get; set; }
    }
}
