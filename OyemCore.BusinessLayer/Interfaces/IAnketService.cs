using System.Collections.Generic;

namespace OyemCore.BusinessLayer.Interfaces
{
    // Anket — sadece OYLAMA. referans: WebServiceAnket (DashboardAnketleriGetir + AnketSoru/Secenek + AnketKaydet).
    // Anket oluşturma/rapor/klonlama web'de kalır; mobile aktarılmaz.
    public interface IAnketService
    {
        // Aktif ve kullanıcının henüz katılmadığı anketler (ana ekran için).
        IEnumerable<object> GetActiveSurveys(int kullaniciID);
        // Bir anketin oylama formu (sorular + seçenekler) + kullanıcı oy verdi mi.
        object GetSurveyDetail(int kullaniciID, int anketID);
        // Oy gönder. answers: her soru için (soruID, secenekID?, cevap?).
        object SubmitVote(int kullaniciID, int anketID, List<AnketAnswer> answers);
    }

    public class AnketAnswer
    {
        public int SoruID { get; set; }
        public int? SecenekID { get; set; }   // Tur=SEC ise
        public string Cevap { get; set; }     // serbest metin ise
    }
}
