using System;

namespace OyemCore.DataLayer.Entities
{
    public class tb_TedDegKalitePuan
    {
        public string BelgeNo { get; set; }
        public string PKod { get; set; }
        public string Deger { get; set; }
        public int? Puan { get; set; }
        public string DegerEtiket { get; set; }
        public DateTime? IslemTar { get; set; }
        public string IslemYapan { get; set; }
    }
}
