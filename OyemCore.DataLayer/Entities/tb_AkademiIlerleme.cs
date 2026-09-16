using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_AkademiIlerleme
    {
        public int IlerlemeID { get; set; }
        public int AtamaID { get; set; }
        public int MaxIzlenenSaniye { get; set; }
        public int AktifIzlemeSaniye { get; set; }
        public bool TamamlandiMi { get; set; }
        public DateTime? TamamlanmaTarihi { get; set; }
        public DateTime GuncellemeTarihi { get; set; }
    }
}
