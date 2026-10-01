using System;

namespace OyemCore.DataLayer.Entities
{
    // Cok onayli hurda sureci — talep basligi. referans: Zimmet/Aygit.html "Hurdaya Ayir" butonu
    public class tb_DemirbasHurdaTalep
    {
        public int HurdaTalepID { get; set; }
        public int AygitID { get; set; }
        public string TalepEdenSicil { get; set; }
        public string Sebep { get; set; }
        public DateTime TalepTarihi { get; set; }
        public string Durum { get; set; } // BEKLEMEDE/ONAYLANDI/REDDEDILDI
        public DateTime? TamamlanmaTarihi { get; set; }
    }
}
