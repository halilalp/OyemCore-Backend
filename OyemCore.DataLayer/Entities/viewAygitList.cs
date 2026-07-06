using System;

namespace OyemCore.DataLayer.Entities
{
    public class viewAygitList
    {
        public int AygitID { get; set; }
        public bool? Durum { get; set; } // Note: Boolean in EF
        public string Kategori { get; set; }
        public int? AygitKategoriID { get; set; }
        public int? UstKatID { get; set; }
        public string UstKatTanim { get; set; }
        public string MarkaAdi { get; set; }
        public int? MarkaID { get; set; }
        public int? Miktar { get; set; }
        public string DepartmanAdi { get; set; }
        public string SeriNo { get; set; }
        public string Konum { get; set; }
        public string Tanim { get; set; }
        public bool? HataBildir { get; set; }
        public string SorumluDepKod { get; set; }
        public string DemirbasKodu { get; set; }
        public string MasrafMerkezi { get; set; }
        public DateTime? AmortismanBitisTar { get; set; }
        public bool? HurdaDurum { get; set; }
        public string ZimmetliSicil { get; set; }
        public string Kod { get; set; }
        public bool? BarkodOnay { get; set; }
        public string KullanimSekli { get; set; }
    }
}
