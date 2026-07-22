using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OyemCore.DataLayer.Entities
{
    // Satın Alma Siparişi kalemi. Referans: tb_SasKalem
    [Table("tb_SasKalem")]
    public class tb_SasKalem
    {
        [Key]
        public int SasKalemID { get; set; }

        public int? SasID { get; set; }

        public string MalzemeKodu { get; set; }

        public decimal? Miktar { get; set; }

        public string BirimKodu { get; set; }

        public decimal? BirimFiyat { get; set; }

        public decimal? ToplamFiyat { get; set; }

        public int? SatKalemID { get; set; }
    }
}
