using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_Ayarlar
    {
        public int ID { get; set; }
        public string Adres { get; set; }
        public string Sifre { get; set; }
        public int? Port { get; set; }
        public int? Sure { get; set; }
        public string Smtp { get; set; }
        public bool? SslDurum { get; set; }
        public string CalismaSekli { get; set; } // "SERVIS" veya "DIREKT"
    }
}
