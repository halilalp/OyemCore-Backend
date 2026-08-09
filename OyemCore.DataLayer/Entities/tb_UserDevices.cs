using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_UserDevices
    {
        public int ID { get; set; }
        public string SicilNo { get; set; } = null!;
        public string PushToken { get; set; } = null!;
        public string? DeviceType { get; set; }
        public DateTime KayitTarihi { get; set; }
        public DateTime SonGirisTarihi { get; set; }
    }
}
