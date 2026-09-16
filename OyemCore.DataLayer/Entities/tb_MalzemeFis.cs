using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_MalzemeFis
    {
        public int FisID { get; set; }
        public string FisNo { get; set; }
        public string HareketTipKodu { get; set; }
        public DateTime FisTarihi { get; set; }
        public string CariKodu { get; set; }
        public string BelgeNo { get; set; }
        public string DepoKodu { get; set; }
        public string HedefDepoKodu { get; set; }
        public string Aciklama { get; set; }
        public string OnayDurumu { get; set; }
        public string KayitSicil { get; set; }
        public DateTime? KayitTar { get; set; }
        public string DosyaUrl { get; set; }
    }
}
