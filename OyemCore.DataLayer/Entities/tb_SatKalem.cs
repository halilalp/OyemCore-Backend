using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OyemCore.DataLayer.Entities
{
    // Satın Alma Talebi kalemi. Referans: tb_SatKalem
    [Table("tb_SatKalem")]
    public class tb_SatKalem
    {
        [Key]
        public int SatKalemID { get; set; }

        public string BelgeNo { get; set; }

        public int? KalemNo { get; set; }

        public string MalzemeKodu { get; set; }

        public decimal? Miktar { get; set; }

        public string BirimKodu { get; set; }

        public string TalepNedeni { get; set; }
    }
}
