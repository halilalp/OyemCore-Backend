using System.Collections.Generic;

namespace OyemCore.BusinessLayer.Interfaces
{
    // Satın Alma (SAT talep / SAS sipariş). Referans: WebServiceSatOnay.cs + WebServiceSas.cs
    public interface ISatSasService
    {
        // Dashboard sayaçları (taslak/bekleyen/onaylı talep, sipariş sayısı)
        object GetDashboard(string sicilNo, string adminBelgeTur);

        // Kullanıcının gördüğü SAT talepleri (kendi + onayına düşen + admin ise tümü), taslak hariç.
        IEnumerable<object> GetSatRequests(string sicilNo, string adminBelgeTur);

        // Kullanıcının açık taslağını getirir; yoksa oluşturur. Kalemleriyle birlikte döner.
        object CheckOrCreateSatDraft(string sicilNo, string eposta);

        // Bir talebin başlık + kalem detayı.
        object GetSatDetail(string belgeNo);

        // Taslağa kalem ekler. Döner: eklenen kalem + güncel liste.
        object AddSatItem(string belgeNo, string malzemeKodu, decimal miktar, string birimKodu, string neden);

        // Taslaktan kalem siler.
        bool DeleteSatItem(int satKalemID);

        // Taslağın konu/açıklamasını kaydeder.
        bool SaveSatHeader(string sicilNo, string konu, string aciklama);

        // Taslağı onaya gönderir (SurecDurum TASLAK -> ilk onay adımı). Faz B.
        bool SubmitSatRequest(string sicilNo, string konu, string aciklama);
    }
}
