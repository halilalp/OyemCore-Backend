using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_KullaniciYetki
    {
        public int KullaniciYetkiID { get; set; }
        public int KullaniciID { get; set; }
        public int SayfaID { get; set; }
        public DateTime? KayitTar { get; set; }
    }
}
