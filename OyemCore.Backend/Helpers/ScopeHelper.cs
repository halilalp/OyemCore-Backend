using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.Backend.Helpers
{
    // Dashboard/rapor endpoint'lerinde "admin olmayan kullanıcı yalnızca kendi şirketinin
    // datasını görür" kuralını sunucu tarafında uygular.
    // KURAL (kullanıcı isteği): tb_Kullanici.AdminBelgeTur içinde "ADMIN" varsa TÜM şirketleri
    // görür; yoksa (BAKIMADMIN, TICKET gibi modül belgeleri dahil) yalnız kendi şirketini görür.
    // AdminBelgeTur formatı webportal'daki gibi yıldızla ayrık ("*IT*ERP*ADMIN*").
    public static class ScopeHelper
    {
        // adminKodlari parametresi geriye dönük uyumluluk için tutuluyor; kurala göre yalnız
        // "ADMIN" belgesi tam yetki verir, diğer modül kodları dikkate alınmaz.
        public static (bool IsAdmin, string OwnSirket) GetCompanyScope(ClaimsPrincipal user, IYbsDbContext ctx, params string[] adminKodlari)
        {
            var sicilNo = user?.FindFirst("SicilNo")?.Value ?? "";
            var adminBelgeTur = user?.FindFirst("AdminBelgeTur")?.Value ?? "";

            // Kural: AdminBelgeTur'unde "ADMIN" ifadesi geçiyorsa (BAKIMADMIN dahil) tüm şirketler.
            bool isAdmin = (adminBelgeTur ?? "").ToUpperInvariant().Contains("ADMIN");

            string ownSirket = string.IsNullOrEmpty(sicilNo)
                ? ""
                : (ctx.tb_Personel.AsNoTracking().Where(p => p.SicilNo == sicilNo).Select(p => p.SirketKodu).FirstOrDefault() ?? "");

            return (isAdmin, ownSirket);
        }
    }
}
