using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OyemCore.DataLayer.Entities
{
    // Satın Alma Siparişi (SAS) başlığı. Referans: tb_SaSip / WebServiceSas.cs
    [Table("tb_SaSip")]
    public class tb_SaSip
    {
        [Key]
        public int SasID { get; set; }

        public string BelgeNo { get; set; }

        public string SatBelgeNo { get; set; }

        public int? TeklifID { get; set; }

        public string TedarikciKodu { get; set; }

        public string KayitSicil { get; set; }

        public DateTime? KayitTar { get; set; }

        public decimal? ToplamTutar { get; set; }

        public string ParaBirim { get; set; }

        public bool? Durum { get; set; }

        public bool? Goster { get; set; }

        public DateTime? SonIslemTar { get; set; }
    }
}
