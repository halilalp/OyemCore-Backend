using System;

namespace OyemCore.DataLayer.Entities
{
    // Demirbas bakim/tamir kaydi. referans: Zimmet/AygitZimmet.html "Bakima Gonder"/"Bakimi Tamamla"
    public class tb_AygitBakim
    {
        public int BakimID { get; set; }
        public int AygitID { get; set; }
        public int TuruID { get; set; } // tb_AygitBakimTuru
        public DateTime BaslangicTar { get; set; }
        public DateTime? BitisTar { get; set; }
        public string Durum { get; set; } // SERVISTE/TAMAMLANDI/IPTAL
        public string Aciklama { get; set; }
        public string ServisFirma { get; set; }
        public decimal? Maliyet { get; set; }
        public string IslemYapanSicil { get; set; }
        public string SonucAciklama { get; set; }
        public DateTime KayitTar { get; set; }
        public string OncekiZimmetliSicil { get; set; } // Bakima gonderilmeden once zimmetli oldugu kisi — tamamlaninca geri verilebilir
    }
}
