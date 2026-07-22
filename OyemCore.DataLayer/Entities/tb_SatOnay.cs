using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OyemCore.DataLayer.Entities
{
    // Satın Alma Talebi (SAT) başlığı. Referans: tb_SatOnay / WebServiceSatOnay.cs
    [Table("tb_SatOnay")]
    public class tb_SatOnay
    {
        [Key]
        public int SatOnayID { get; set; }

        public string BelgeNo { get; set; }

        public string KayitSicil { get; set; }

        public string KayitEposta { get; set; }

        public string Konu { get; set; }

        public string Aciklama { get; set; }

        public DateTime? KayitTar { get; set; }

        public bool? Durum { get; set; }

        public string SurecDurum { get; set; }

        public string BekleyenOnay { get; set; }

        public string SonDurumBilgi { get; set; }

        public int? OnayTeklifID { get; set; }

        public string DosyaUrl { get; set; }

        public string KurBilgi { get; set; }

        public DateTime? SonIslemTar { get; set; }

        public DateTime? GmOnayTar { get; set; }
    }
}
