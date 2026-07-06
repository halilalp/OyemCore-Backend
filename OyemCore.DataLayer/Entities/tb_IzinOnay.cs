using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_IzinOnay
    {
        public int IzinOnayID { get; set; }
        public string BelgeNo { get; set; }
        public string IzinTuru { get; set; }
        public string Aciklama { get; set; }
        public string KayitSicil { get; set; }
        public string KayitEposta { get; set; }
        public DateTime? CikisTar { get; set; }
        public DateTime? IsBasiTar { get; set; }
        public double? IsGunu { get; set; }
        public DateTime? KayitTar { get; set; }
        public bool? Durum { get; set; }
        public string SurecDurum { get; set; }
        public string BekleyenOnay { get; set; }
        public string SonDurumBilgi { get; set; }
    }
}
