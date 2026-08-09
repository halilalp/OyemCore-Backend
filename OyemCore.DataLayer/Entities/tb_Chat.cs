using System;

namespace OyemCore.DataLayer.Entities
{
    // Chat mesajı (1:1 veya grup). referans: webportal tb_Chat / WebServiceChat
    public class tb_Chat
    {
        public int ID { get; set; }
        public string GonderenSicilNo { get; set; }
        public string AliciSicilNo { get; set; }   // 1:1 sicil veya "GROUP_..." grup kodu
        public string MesajMetni { get; set; }
        public string DosyaAdi { get; set; }
        public string DosyaYolu { get; set; }
        public string DosyaTipi { get; set; }
        public long? DosyaBoyutu { get; set; }
        public DateTime GonderimTarihi { get; set; }
        public bool Okundu { get; set; }
        public DateTime? OkunmaTarihi { get; set; }
        public int? ParentID { get; set; }         // yanıtlanan mesaj
    }
}
