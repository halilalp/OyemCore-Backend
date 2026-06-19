using System;

namespace WebPortalSpace.BusinessLayer.Dtos
{
    public class MakineDto
    {
        public string MakineKodu { get; set; }
        public string MakineAdi { get; set; }
        public string BolumKodu { get; set; }
        public string SirketKodu { get; set; }
        public bool? Durum { get; set; }
        public string BolumAdi { get; set; }
        public string SirketAdi { get; set; }
    }
}
