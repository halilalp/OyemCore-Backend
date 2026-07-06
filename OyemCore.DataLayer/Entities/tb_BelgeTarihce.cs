using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_BelgeTarihce
    {
        public int BelgeTarihceID { get; set; }
        public string BelgeKodu { get; set; }
        public string Konu { get; set; }
        public string Aciklama { get; set; }
        public DateTime? KayitTar { get; set; }
    }
}
