using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_Sms
    {
        public int SmsID { get; set; }
        public string Alan { get; set; }
        public string AlanTlf { get; set; }
        public string Gonderen { get; set; }
        public string Icerik { get; set; }
        public DateTime? KayitTarih { get; set; }
        public string Konu { get; set; }
        public bool? Durum { get; set; }
        public int? TryCount { get; set; }
        public DateTime? GonTarih { get; set; }
    }
}
