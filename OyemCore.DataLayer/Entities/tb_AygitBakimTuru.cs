namespace OyemCore.DataLayer.Entities
{
    // Bakim/tamir islem turu listesi (Bakim/Tamir/Kalibrasyon/...), admin tarafindan yonetilir. referans: Admin/DemirbasAyarlari.html
    public class tb_AygitBakimTuru
    {
        public int TuruID { get; set; }
        public string Tanim { get; set; }
        public bool AktifMi { get; set; }
        public int? Sira { get; set; }
    }
}
