using System;

namespace OyemCore.DataLayer.Entities
{
    // Talep acilirken o anki aktif onaylayicilarin anlik goruntusu (snapshot) olarak olusturulur —
    // her onaylayici kendi satirini bagimsiz olarak ONAY/RET yapar. referans: tb_DemirbasHurdaTalep
    public class tb_DemirbasHurdaOnay
    {
        public int OnayID { get; set; }
        public int HurdaTalepID { get; set; }
        public string OnaylayanSicil { get; set; }
        public string Durum { get; set; } // BEKLEMEDE/ONAY/RET
        public string RedSebebi { get; set; }
        public DateTime? KararTarihi { get; set; }
    }
}
