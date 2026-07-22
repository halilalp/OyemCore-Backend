using System.Collections.Generic;

namespace OyemCore.BusinessLayer.Interfaces
{
    // Proje / Toplantı yönetimi. Referans: ServiceToplantiIslemleri.
    // Faz 1 — okuma: liste + detay (katılımcı/görev/dosya).
    public interface IProjeToplantiService
    {
        // Kullanıcının gördüğü proje/toplantılar: sahibi + katılımcı + görev sorumlusu +
        // proje türü admini. tur: '' | 'P' | 'T'; durum: '' | 'TAMAMLANDI' | 'BEKLEMEDE'.
        IEnumerable<object> GetList(int userId, string konu, string durum, string tur);

        // Başlık + katılımcılar + görevler + dosyalar.
        object GetDetail(int userId, int toplantiId);
    }
}
