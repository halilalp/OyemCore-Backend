using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_Hiyerarsi
    {
        public int HiyerarsiID { get; set; }
        public string Eposta { get; set; }
        public string SicilNo { get; set; }
        public string Amir1 { get; set; }
        public string Amir2 { get; set; }
        public string Amir3 { get; set; }
        public int? izin { get; set; }
    }
}
