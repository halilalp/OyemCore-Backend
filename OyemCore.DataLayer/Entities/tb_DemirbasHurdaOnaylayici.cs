using System;

namespace OyemCore.DataLayer.Entities
{
    // Hurda taleplerine onay verecek en fazla 3 kisilik, hiyerarsik olmayan liste. referans: Admin/DemirbasAyarlari.html
    public class tb_DemirbasHurdaOnaylayici
    {
        public int OnaylayiciID { get; set; }
        public string SicilNo { get; set; }
        public bool AktifMi { get; set; }
        public DateTime EklenmeTarihi { get; set; }
    }
}
