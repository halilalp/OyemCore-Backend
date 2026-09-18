using System;

namespace OyemCore.DataLayer.Entities
{
    // Sinav tekrar hakki talebi — personel hakki bittiginde sebep yazarak talep eder,
    // AKADEMI admin belge turune sahip yonetici onaylar/reddeder.
    public class tb_AkademiSinavTalep
    {
        public int TalepID { get; set; }
        public int AtamaID { get; set; }
        public string TalepSebebi { get; set; } = null!;
        public DateTime TalepTarihi { get; set; }
        public string Durum { get; set; } = null!; // BEKLEMEDE / ONAYLANDI / REDDEDILDI
        public string? RedSebebi { get; set; }
        public string? CevaplayanSicil { get; set; }
        public DateTime? CevapTarihi { get; set; }
    }
}
