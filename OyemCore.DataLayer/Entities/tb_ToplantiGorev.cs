using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OyemCore.DataLayer.Entities
{
    // Toplantı/proje görevi (görevlendirme). Referans: tb_ToplantiGorev
    [Table("tb_ToplantiGorev")]
    public class tb_ToplantiGorev
    {
        [Key]
        public int ID { get; set; }

        public int? ToplantiID { get; set; }
        public int? GorevNo { get; set; }
        public string Aciklama { get; set; }
        public string SorumluEposta { get; set; }
        public DateTime? KayitTar { get; set; }
        public string KayitEposta { get; set; }
        public DateTime? TerminTar { get; set; }
        public bool? Durum { get; set; }
        public DateTime? OnayTar { get; set; }
        public bool? GoruntuDurum { get; set; }
        public int? RevizyonAdet { get; set; }
        public DateTime? BaslamaTarihi { get; set; }
        public string TrlSeviyeKodu { get; set; }
    }
}
