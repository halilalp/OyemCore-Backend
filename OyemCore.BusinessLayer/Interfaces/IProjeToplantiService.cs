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

        // Yeni proje/toplantı. tur: 'P'|'T'. katilimciEpostalar başlangıç katılımcıları.
        int Create(int userId, string tur, string projeTur, string konu, string aciklama,
                   string basTarih, string bitTarih, List<string> katilimciEpostalar);

        // Proje/toplantı durumunu değiştirir (tamamla/beklemede). Yalnız oluşturan.
        void UpdateDurum(int userId, int toplantiId, bool durum);

        // Görev ekle (yalnız oluşturan veya katılımcı). Proje tarih aralığı doğrulanır.
        int AddGorev(int userId, int toplantiId, string aciklama, string sorumluEposta,
                     string terminTar, string baslamaTar, string trl);

        // Görevi tamamla (yalnız oluşturan veya görevi ekleyen).
        void CompleteGorev(int userId, int gorevId);

        void DeleteGorev(int userId, int gorevId);

        // Katılımcı ekle/çıkar (yalnız oluşturan).
        void AddKatilimci(int userId, int toplantiId, string eposta);
        void RemoveKatilimci(int userId, int katilimciId);
    }
}
