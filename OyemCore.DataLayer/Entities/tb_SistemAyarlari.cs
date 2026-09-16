using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_SistemAyarlari
    {
        public string AyarKey { get; set; }
        public string AyarValue { get; set; }
        public DateTime? GuncellemeTarihi { get; set; }
        public string GuncelleyenSicil { get; set; }
    }
}
