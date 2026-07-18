using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.Backend.Helpers
{
    // Dashboard/rapor endpoint'lerinde "admin olmayan kullanıcı yalnızca kendi şirketinin
    // datasını görür" kuralını sunucu tarafında uygular. AdminBelgeTur formatı webportal'daki
    // gibi yıldızla ayrık ("*IT*ERP*BAKIMADMIN*"). adminKodlari verilen modül için tam-yetki
    // (tüm şirketleri görme) sağlayan belge türleridir; "ADMIN" her zaman tam yetkilidir.
    public static class ScopeHelper
    {
        public static (bool IsAdmin, string OwnSirket) GetCompanyScope(ClaimsPrincipal user, IYbsDbContext ctx, params string[] adminKodlari)
        {
            var sicilNo = user?.FindFirst("SicilNo")?.Value ?? "";
            var adminBelgeTur = user?.FindFirst("AdminBelgeTur")?.Value ?? "";

            var tokens = adminBelgeTur
                .Split(new[] { '*' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().ToUpperInvariant())
                .ToList();

            bool isAdmin = tokens.Contains("ADMIN")
                || (adminKodlari != null && adminKodlari.Any(c => tokens.Contains((c ?? "").ToUpperInvariant())));

            string ownSirket = string.IsNullOrEmpty(sicilNo)
                ? ""
                : (ctx.tb_Personel.AsNoTracking().Where(p => p.SicilNo == sicilNo).Select(p => p.SirketKodu).FirstOrDefault() ?? "");

            return (isAdmin, ownSirket);
        }
    }
}
