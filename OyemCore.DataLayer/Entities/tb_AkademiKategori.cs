namespace OyemCore.DataLayer.Entities
{
    // Akademi icerik kategorileri (serbest metin yerine DB'den secilen liste).
    public class tb_AkademiKategori
    {
        public int KategoriID { get; set; }
        public string Ad { get; set; } = null!;
        public bool AktifMi { get; set; }
    }
}
