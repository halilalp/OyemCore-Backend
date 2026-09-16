using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.Backend.Authorization
{
    // Gerçek sunucu-taraflı, sayfa-bazlı yetki kontrolü — WebPortal'daki ClsYetki.SayfaYetkiKontrol()'ün
    // karşılığı. Mobil client zaten aynı kontrolü kendi tarafında yapıyor (bkz. bakimYetki.ts,
    // useAdminAccess.ts) ama bu OLMADAN [Authorize] tek başına "geçerli token'ı olan herkes" bu
    // endpoint'i çağırabilir demekti. tb_KullaniciYetki + tb_Sayfa üzerinden kullanıcının GERÇEKTEN
    // bu sayfaya (SayfaUrl ile eşleşen) atanmış olup olmadığını DB'den doğrular.
    public class RequiresSayfaYetkisiAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _sayfaUrls;

        // Birden fazla sayfaUrl verilebilir (ör. Bakım Planı hem "plan" hem "uygula" modunda aynı
        // route'a gelir ama farklı tb_Sayfa kayıtlarına karşılık gelir) — herhangi biriyle eşleşme yeterli.
        public RequiresSayfaYetkisiAttribute(params string[] sayfaUrls)
        {
            _sayfaUrls = sayfaUrls;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim == null || !int.TryParse(idClaim.Value, out int kullaniciId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var dbContext = context.HttpContext.RequestServices.GetService(typeof(IYbsDbContext)) as IYbsDbContext;
            if (dbContext == null)
            {
                context.Result = new StatusCodeResult(500);
                return;
            }

            bool hasAccess = (from y in dbContext.tb_KullaniciYetki
                               join s in dbContext.tb_Sayfa on y.SayfaID equals s.SayfaID
                               where y.KullaniciID == kullaniciId
                                  && s.Durum == true
                                  && _sayfaUrls.Contains(s.SayfaUrl)
                               select y.KullaniciID).Any();

            if (!hasAccess)
            {
                context.Result = new ObjectResult(new { message = "Bu sayfayı görüntülemek için yetkiniz bulunmamaktadır." })
                {
                    StatusCode = 403
                };
            }
        }
    }
}
