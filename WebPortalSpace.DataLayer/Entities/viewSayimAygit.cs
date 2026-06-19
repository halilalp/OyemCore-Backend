using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class viewSayimAygit
    {
        public int AygitID { get; set; }
        public string Tanim { get; set; }
        public string SeriNo { get; set; }
        public bool? Durum { get; set; }
        public int? MarkaID { get; set; }
        public string MarkaAdi { get; set; }
        public int? AygitKategoriID { get; set; }
        public string Kategori { get; set; }
        public int? UstKatID { get; set; }
        public string UstKatTanim { get; set; }
        public string Kod { get; set; }
        public string Konum { get; set; }
        public int? Miktar { get; set; }
        public string SorumluDepKod { get; set; }
        public string DepartmanAdi { get; set; }
        public string DemirbasKodu { get; set; }
        public DateTime? AmortismanBitisTar { get; set; }
        public bool? HataBildir { get; set; }
        public string KullanimSekli { get; set; }
        public bool? HurdaDurum { get; set; }
        public bool? BarkodOnay { get; set; }
        public bool? AktifAygit { get; set; }
        public string MasrafMerkezi { get; set; }
        public string ZimmetliSicil { get; set; }
        public string ZimmetAciklama { get; set; }
        public string SicilNo { get; set; }
        public DateTime? IslemTar { get; set; }
    }
}
