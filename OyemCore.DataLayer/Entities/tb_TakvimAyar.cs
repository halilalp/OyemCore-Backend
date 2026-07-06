using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OyemCore.DataLayer.Entities
{
    [Table("tb_TakvimAyar")]
    public class tb_TakvimAyar
    {
        [Key]
        public int AyarID { get; set; }
        
        [StringLength(250)]
        public string? Konu { get; set; }
        
        public bool? Durum { get; set; }
        public bool? Katilimci { get; set; }
        
        [StringLength(50)]
        public string? BgColor { get; set; }
        
        [StringLength(50)]
        public string? BrColor { get; set; }
    }
}
