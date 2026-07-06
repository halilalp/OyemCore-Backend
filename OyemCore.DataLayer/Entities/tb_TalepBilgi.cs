using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_TalepBilgi
    {
        public int TalepBilgiID { get; set; }
        public string TalepKodu { get; set; }
        public string KayitSicil { get; set; }
        public string BilgiSicil { get; set; }
        public DateTime? KayitTar { get; set; }
    }
}
