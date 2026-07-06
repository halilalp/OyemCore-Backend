using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace OyemCore.DataLayer.Entities
{
    public class tb_Makine
    {
        public string MakineKodu { get; set; }
        public string MakineAdi { get; set; }
        public string BolumKodu { get; set; }
        
        [NotMapped]
        public string SirketKodu { get; set; }
        public bool? Durum { get; set; }
    }
}
