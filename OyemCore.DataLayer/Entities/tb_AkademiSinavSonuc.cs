using System;

namespace OyemCore.DataLayer.Entities
{
    // Tamamlanmis sinav denemesinin kalici sonucu (raporlama icin).
    public class tb_AkademiSinavSonuc
    {
        public int SonucID { get; set; }
        public int AtamaID { get; set; }
        public int OturumID { get; set; }
        public int DogruSayisi { get; set; }
        public int ToplamSoru { get; set; }
        public int PuanYuzdesi { get; set; }
        public bool BasariliMi { get; set; }
        public int SekmeDegisimSayisi { get; set; }
        public DateTime BaslangicZamani { get; set; }
        public DateTime BitisZamani { get; set; }
    }
}
