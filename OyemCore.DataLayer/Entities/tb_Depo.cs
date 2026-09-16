using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_Depo
    {
        public string DepoKodu { get; set; }
        public string DepoAdi { get; set; }
        public string DepoTipiKodu { get; set; }
        public bool? Aktif { get; set; }
    }
}
