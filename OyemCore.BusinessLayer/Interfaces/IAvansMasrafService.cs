using System.Collections.Generic;

namespace OyemCore.BusinessLayer.Interfaces
{
    // Mağaza avans-masraf onay iş akışı. referans: WebServiceAvansMasraf
    public interface IAvansMasrafService
    {
        object AvansKaydet(int kullaniciID, int id, decimal tutar, string aciklama);
        object MasrafKaydet(int kullaniciID, int id, decimal toplamTutar, string aciklama, int? iliskiliAvansID, IEnumerable<MasrafKalemDto> kalemler);
        IEnumerable<object> AvansListesiGetir(int kullaniciID);
        IEnumerable<object> MasrafListesiGetir(int kullaniciID);
        object MasrafDetayGetir(int kullaniciID, int masrafID);
        IEnumerable<object> OnayBekleyenTaleplerGetir(int kullaniciID);
        object AvansMasrafOnaylaReddet(int kullaniciID, string tip, int id, bool onay, string aciklama);
    }

    public class MasrafKalemDto
    {
        public string FisNo { get; set; }
        public string Firma { get; set; }
        public System.DateTime? Tarih { get; set; }
        public decimal Tutar { get; set; }
        public decimal KdvTutar { get; set; }
        public string Aciklama { get; set; }
        public string DosyaYolu { get; set; }
    }
}
