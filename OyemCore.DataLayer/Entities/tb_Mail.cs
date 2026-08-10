using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_Mail
    {
        public int MailID { get; set; }
        public string Gonderen { get; set; }
        public string AlanEPosta { get; set; }
        public string Konu { get; set; }
        public string Icerik { get; set; }
        public DateTime? KayitTarih { get; set; }
        public int? TryCount { get; set; }
        public bool? Durum { get; set; }
        public DateTime? GonTarih { get; set; }
        public string HataKodu { get; set; }
        public string Ek1 { get; set; }
        public string Ek2 { get; set; }
        public string Ek3 { get; set; }
        public string Ek4 { get; set; }
    }
}
