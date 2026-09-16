using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_MalzemeHareketTip
    {
        public string HareketTipKodu { get; set; }
        public string HareketTipAdi { get; set; }
        public bool GirisMi { get; set; }
        public bool CikisMi { get; set; }
        public bool MaliyetEtkisiVarMi { get; set; }
        public bool UretimIlgiliMi { get; set; }
        public bool BakimIlgiliMi { get; set; }
        public string Aciklama { get; set; }
        public bool Aktif { get; set; }
    }
}
