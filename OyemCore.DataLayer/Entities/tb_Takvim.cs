using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OyemCore.DataLayer.Entities
{
    [Table("tb_Takvim")]
    public class tb_Takvim
    {
        [Key]
        public int TakvimID { get; set; }
        
        public int? AyarID { get; set; }
        public int? MasterID { get; set; }
        
        [StringLength(250)]
        public string? Konu { get; set; }
        
        [StringLength(50)]
        public string? KayitSicil { get; set; }
        
        public DateTime? BasTar { get; set; }
        public DateTime? BitTar { get; set; }
        
        public string? Katilimci { get; set; }
        public string? Aciklama { get; set; }
    }
}
