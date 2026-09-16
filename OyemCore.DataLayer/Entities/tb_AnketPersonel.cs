using System;

namespace OyemCore.DataLayer.Entities
{
    // Ankete katılan personel (kişi başına 1 kez). Bileşik PK: (AnketID, SicilNo). referans: webportal tb_AnketPersonel
    public class tb_AnketPersonel
    {
        public int AnketID { get; set; }
        public string SicilNo { get; set; }
        public DateTime? KayitTar { get; set; }
        public string IP { get; set; }
        public DateTime? DogumTar { get; set; }
    }
}
