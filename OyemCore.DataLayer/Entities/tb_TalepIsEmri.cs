using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OyemCore.DataLayer.Entities
{
    [Table("tb_TalepIsEmri")]
    public class tb_TalepIsEmri
    {
        [Key]
        public int TalepIsEmriID { get; set; }
        
        public string TalepKodu { get; set; }
        
        public int? IsEmriTurID { get; set; }
        
        public string Aciklama { get; set; }
        
        public DateTime? TerminTar { get; set; }
        
        public string Sicil { get; set; }
        
        public DateTime? KayitTar { get; set; }
        
        public string DosyaUrl { get; set; }
        
        public string DosyaUrl2 { get; set; }
        
        public DateTime? KapanmaTar { get; set; }
        
        public string SonAciklama { get; set; }
        
        public double? IslemSure { get; set; }
        
        public bool? Durum { get; set; }
    }
}
