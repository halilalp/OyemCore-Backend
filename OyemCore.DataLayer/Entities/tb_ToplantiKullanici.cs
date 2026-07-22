using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OyemCore.DataLayer.Entities
{
    // Toplantı/proje katılımcısı. Referans: tb_ToplantiKullanici
    [Table("tb_ToplantiKullanici")]
    public class tb_ToplantiKullanici
    {
        [Key]
        public int ID { get; set; }

        public int? ToplantiID { get; set; }
        public string KullaniciEposta { get; set; }
    }
}
