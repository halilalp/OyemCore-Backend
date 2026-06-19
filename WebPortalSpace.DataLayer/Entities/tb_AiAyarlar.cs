using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_AiAyarlar
    {
        public int ID { get; set; }
        public string AyarAdi { get; set; }
        public string Provider { get; set; }
        public string Model { get; set; }
        public string ApiKey { get; set; }
        public string SistemPrompt { get; set; }
        public int? MaksimumToken { get; set; }
        public bool? Aktif { get; set; }
        public int? SiraNo { get; set; }
        public DateTime? KayitTarihi { get; set; }
    }
}
