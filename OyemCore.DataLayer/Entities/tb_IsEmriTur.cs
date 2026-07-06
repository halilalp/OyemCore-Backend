using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OyemCore.DataLayer.Entities
{
    [Table("tb_IsEmriTur")]
    public class tb_IsEmriTur
    {
        [Key]
        public int IsEmriTurID { get; set; }
        
        public string Tanim { get; set; }
        
        public bool? Durum { get; set; }
    }
}
