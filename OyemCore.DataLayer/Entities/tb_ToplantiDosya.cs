using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OyemCore.DataLayer.Entities
{
    // Toplantı/proje ekli dosyası. Referans: tb_ToplantiDosya
    [Table("tb_ToplantiDosya")]
    public class tb_ToplantiDosya
    {
        [Key]
        public int ID { get; set; }

        public int? ToplantiID { get; set; }
        public string DosyaBaslik { get; set; }
        public string Aciklama { get; set; }
        public string DosyaUrl { get; set; }
        public DateTime? KayitTar { get; set; }
        public string KayitEposta { get; set; }
    }
}
