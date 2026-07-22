using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OyemCore.DataLayer.Entities
{
    // Proje türü tanımları (dropdown). Referans: tb_RecProjeTur
    [Table("tb_RecProjeTur")]
    public class tb_RecProjeTur
    {
        [Key]
        public int RecProjeTurID { get; set; }

        public string Tanim { get; set; }
        public bool? Durum { get; set; }
    }
}
