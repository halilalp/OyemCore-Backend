using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_BelgeOnay
    {
        public int BelgeOnayID { get; set; }
        public string BelgeNo { get; set; }
        public short? Sira { get; set; }
        public string OnaySicil { get; set; }
        public bool? Durum { get; set; }
        public string Aciklama { get; set; }
        public DateTime? IslemTar { get; set; }
        public string OnayTur { get; set; }
    }
}
