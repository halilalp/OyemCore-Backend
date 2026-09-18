using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OyemCore.BusinessLayer.Common;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.Backend.Helpers
{
    // Dashboard/rapor endpoint'lerinde "admin olmayan kullanıcı yalnızca kendi şirketinin
    // datasını görür" kuralını sunucu tarafında uygular.
    // KURAL (güncellendi — bkz. AdminBelgeTuruHelper): SADECE gerçek "ADMIN" belgesi tüm
    // şirketleri görür. BAKIMADMIN/STOKADMIN/MALZEMEADMIN gibi modül-özel "*ADMIN" kodları
    // ARTIK otomatik süper-admin sayılmıyor — bu, eski ".Contains(\"ADMIN\")" alt-string
    // kontrolünün istemeden verdiği geniş yetkiydi (bkz. güvenlik incelemesi).
    public static class ScopeHelper
    {
        // adminKodlari parametresi geriye dönük uyumluluk için tutuluyor; kurala göre yalnız
        // "ADMIN" belgesi tam yetki verir, diğer modül kodları dikkate alınmaz.
        public static (bool IsAdmin, string OwnSirket) GetCompanyScope(ClaimsPrincipal user, IYbsDbContext ctx, params string[] adminKodlari)
        {
            var sicilNo = user?.FindFirst("SicilNo")?.Value ?? "";
            var adminBelgeTur = user?.FindFirst("AdminBelgeTur")?.Value ?? "";

            bool isAdmin = AdminBelgeTuruHelper.IsAdmin(adminBelgeTur);

            string ownSirket = string.IsNullOrEmpty(sicilNo)
                ? ""
                : (ctx.tb_Personel.AsNoTracking().Where(p => p.SicilNo == sicilNo).Select(p => p.SirketKodu).FirstOrDefault() ?? "");

            return (isAdmin, ownSirket);
        }
    }
}
