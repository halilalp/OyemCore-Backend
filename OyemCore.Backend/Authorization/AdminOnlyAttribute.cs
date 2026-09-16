using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OyemCore.Backend.Authorization
{
    // Gerçek sunucu-taraflı yetki kontrolü. Mobil uygulama (AdminAyarlarScreen.tsx ve alt admin
    // ekranları) aynı kuralı zaten client-side'da uyguluyor — buradaki kontrol OLMADAN, [Authorize]
    // tek başına "geçerli bir token'ı olan HERKES" bu endpoint'lere erişebilir demekti (mobil UI'ı
    // hiç kullanmadan, sadece kendi geçerli token'ıyla doğrudan API'ye istek atarak). WebPortal'da
    // bu işi ClsYetki.SayfaYetkiKontrol() sunucu tarafında yapıyordu; burası onun karşılığı.
    public class AdminOnlyAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Class-level [AdminOnly] altında tekil olarak [AllowAnyAuthenticated] ile işaretlenmiş
            // action'lar bu kontrolden muaf tutulur (bkz. o attribute'un XML açıklaması).
            if (context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor cad
                && cad.MethodInfo.GetCustomAttributes(typeof(AllowAnyAuthenticatedAttribute), true).Length > 0)
            {
                return;
            }

            var user = context.HttpContext.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            bool yonetici = string.Equals(user.FindFirst("Yonetici")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            bool zimmetSorumlusu = string.Equals(user.FindFirst("ZimmetSorumlusu")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            bool adminKullanici = string.Equals(user.FindFirst("KullaniciAdi")?.Value, "admin", StringComparison.OrdinalIgnoreCase);

            if (!yonetici && !zimmetSorumlusu && !adminKullanici)
            {
                context.Result = new ObjectResult(new { message = "Bu işlem için yönetici yetkisi gereklidir." })
                {
                    StatusCode = 403
                };
            }
        }
    }
}
