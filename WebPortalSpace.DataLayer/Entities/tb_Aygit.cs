using System;

namespace WebPortalSpace.DataLayer.Entities
{
    public class tb_Aygit
    {
        public int AygitID { get; set; }
        public string Tanim { get; set; }
        public string SeriNo { get; set; }
        public string Aciklama { get; set; }
        public int? AygitKategoriID { get; set; }
        public int? MarkaID { get; set; }
        public int? Miktar { get; set; }
        public string SorumluDepKod { get; set; }
        public bool? HataBildir { get; set; }
        public bool? AktifAygit { get; set; }
        public string DemirbasKodu { get; set; }
        public string Konum { get; set; }
        public string MasrafMerkezi { get; set; }
        public bool? Durum { get; set; }
        public string KullanimSekli { get; set; }
        public DateTime? KayitTar { get; set; }
        public string ZimmetliSicil { get; set; }
        public bool? HurdaDurum { get; set; }
        public bool? BarkodOnay { get; set; }
        public string Ozellik1 { get; set; }
        public string Ozellik2 { get; set; }
        public string Ozellik3 { get; set; }
        public string Ozellik4 { get; set; }
    }
}
