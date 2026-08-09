using System;
using System.Collections.Generic;

namespace OyemCore.BusinessLayer.Dtos
{
    // referans: WebServiceChat DTO'ları (birebir)
    public class UserChatDto
    {
        public int KullaniciID { get; set; }
        public string AdSoyad { get; set; }
        public string Unvan { get; set; }
        public string SicilNo { get; set; }
        public string Cinsiyet { get; set; }
        public string LastMessage { get; set; }
        public DateTime? LastMessageDate { get; set; }
        public int UnreadCount { get; set; }
        public bool IsOnline { get; set; }
        public string OlusturanSicilNo { get; set; }
    }

    public class ChatGroupMemberDto
    {
        public string GroupCode { get; set; }
        public string SicilNo { get; set; }
        public string AdSoyad { get; set; }
    }

    public class SharedFileDto
    {
        public string DosyaAdi { get; set; }
        public string DosyaYolu { get; set; }
        public string DosyaTipi { get; set; }
        public long DosyaBoyutu { get; set; }
        public DateTime GonderimTarihi { get; set; }
        public string GonderenSicilNo { get; set; }
    }
}
