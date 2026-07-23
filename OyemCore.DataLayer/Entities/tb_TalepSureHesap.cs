using System;

namespace OyemCore.DataLayer.Entities
{
    // Talep tamamlandığında MTTR süre kırılımı audit logu (referans: WebServiceBakim.TalepSureHesaplaInternal).
    // Tur: "SONUC" = birleştirilmiş net kesinti (özet), "ONAY"/"SORUCEVAP"/"ISEMRI" = ham kalem.
    // SatirTipi: 1 = özet (SONUC) satırı, 0 = ham detay satırı (referanstaki bool true/false karşılığı).
    public class tb_TalepSureHesap
    {
        public int ID { get; set; }
        public string TalepKodu { get; set; }
        public DateTime BasTar { get; set; }
        public DateTime BitTar { get; set; }
        public string Tur { get; set; }
        public string Aciklama { get; set; }
        public int? RefID { get; set; }
        public int? GrupID { get; set; }
        public int? NetSure { get; set; }
        public int? SatirTipi { get; set; }
    }
}
