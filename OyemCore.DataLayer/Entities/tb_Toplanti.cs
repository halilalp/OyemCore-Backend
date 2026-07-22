using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OyemCore.DataLayer.Entities
{
    // Proje / Toplantı kaydı. Tur = 'TOPLANTI' | 'PROJE'. Referans: tb_Toplanti /
    // ServiceToplantiIslemleri. Kaynak planlama alanları (IsGucu/Maliyet) Faz 2.
    [Table("tb_Toplanti")]
    public class tb_Toplanti
    {
        [Key]
        public int ID { get; set; }

        public string Konu { get; set; }
        public string Aciklama { get; set; }
        public bool? Durum { get; set; }
        public string KullaniciEposta { get; set; }
        public int? OdaID { get; set; }
        public int? Sure { get; set; }
        public string OutlookID { get; set; }
        public string Tur { get; set; }
        public string ProjeTur { get; set; }
        public DateTime? BasTarih { get; set; }
        public DateTime? BitTarih { get; set; }
        public DateTime? KayitTar { get; set; }
        public string SonIslemTar { get; set; }
        public DateTime? DurumTar { get; set; }

        // Faz 2 — kaynak planlama
        public string Sunucu { get; set; }
        public string Url { get; set; }
        public string Platform { get; set; }
        [Column("Veritabanı")]
        public string Veritabani { get; set; }
        public string Amac { get; set; }
        public int? IsGucuTermin { get; set; }
        public decimal? AdamGunUcret { get; set; }
        public int? ToplamIsGucu { get; set; }
        public decimal? ToplamMaliyet { get; set; }
    }
}
