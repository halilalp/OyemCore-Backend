namespace OyemCore.Backend.Authorization
{
    // AdminController üzerinde class-level [AdminOnly] uygulanmış olsa bile, bu attribute ile
    // işaretlenen tekil action'lar bunun dışında tutulur (bkz. AdminOnlyAttribute.OnAuthorization).
    // Kullanım örneği: GetBelgeTarihcePaged — Bakım Planı/Periyodik Kontrol ekranlarında ADMIN
    // OLMAYAN kullanıcılar tarafından da (belge geçmişi görüntülemek için) çağrılıyor.
    public class AllowAnyAuthenticatedAttribute : Attribute
    {
    }
}
