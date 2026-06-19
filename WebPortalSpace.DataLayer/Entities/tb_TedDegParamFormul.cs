using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_TedDegParamFormul
    {
        public int ID { get; set; }
        public string PKod { get; set; }
        public string TurKod { get; set; }
        public string TanimEtiket { get; set; }
        public string Formul1 { get; set; }
        public double? Deger1 { get; set; }
        public string Formul2 { get; set; }
        public double? Deger2 { get; set; }
        public int? Puan { get; set; }
    }
}
