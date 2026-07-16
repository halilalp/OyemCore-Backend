using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OyemCore.DataLayer.Interfaces;
using OyemCore.DataLayer.Entities;

namespace OyemCore.DataLayer.Contexts
{
    public class YbsDbContext : DbContext, IYbsDbContext
    {
        public YbsDbContext(DbContextOptions<YbsDbContext> options) : base(options)
        {
        }

        public DbSet<tb_Kullanici> tb_Kullanici { get; set; }
        public DbSet<tb_Ticket> tb_Ticket { get; set; }
        public DbSet<tb_TicketDosya> tb_TicketDosya { get; set; }
        public DbSet<tb_TicketAciklama> tb_TicketAciklama { get; set; }
        public DbSet<tb_TicketKategori> tb_TicketKategori { get; set; }
        public DbSet<tb_BelgeTarihce> tb_BelgeTarihce { get; set; }
        public DbSet<tb_BelgeOnay> tb_BelgeOnay { get; set; }
        public DbSet<tb_Personel> tb_Personel { get; set; }

        public DbSet<tb_Sirket> tb_Sirket { get; set; }
        public DbSet<tb_Bolum> tb_Bolum { get; set; }
        public DbSet<tb_Hat> tb_Hat { get; set; }
        public DbSet<tb_Makine> tb_Makine { get; set; }
        public DbSet<tb_Malzeme> tb_Malzeme { get; set; }
        public DbSet<tb_IzinOnay> tb_IzinOnay { get; set; }
        public DbSet<tb_Talep> tb_Talep { get; set; }
        public DbSet<tb_IsEmriTur> tb_IsEmriTur { get; set; }
        public DbSet<tb_TalepIsEmri> tb_TalepIsEmri { get; set; }
        public DbSet<tb_TalepBakim> tb_TalepBakim { get; set; }
        public DbSet<tb_TalepGelisme> tb_TalepGelisme { get; set; }
        public DbSet<tb_TalepKategori> tb_TalepKategori { get; set; }
        public DbSet<tb_TalepAmir> tb_TalepAmir { get; set; }
        public DbSet<tb_TalepBilgi> tb_TalepBilgi { get; set; }
        public DbSet<tb_TalepSoruCevap> tb_TalepSoruCevap { get; set; }
        public DbSet<tb_TalepAyar> tb_TalepAyar { get; set; }
        public DbSet<tb_Log> tb_Log { get; set; }
        public DbSet<tb_Sms> tb_Sms { get; set; }
        public DbSet<tb_Hiyerarsi> tb_Hiyerarsi { get; set; }
        public DbSet<tb_Haber> tb_Haber { get; set; }
        public DbSet<tb_Egitim> tb_Egitim { get; set; }
        public DbSet<tb_EgitimKategori> tb_EgitimKategori { get; set; }
        public DbSet<tb_Departman> tb_Departman { get; set; }
        public DbSet<tb_Unvan> tb_Unvan { get; set; }
        public DbSet<SpBakimTalep> SpBakimTalep { get; set; }
        public DbSet<tb_BakimPerKontrol> tb_BakimPerKontrol { get; set; }
        public DbSet<tb_BakimPerKontrolDetay> tb_BakimPerKontrolDetay { get; set; }
        public DbSet<tb_BakimPerKontrolSarfiyat> tb_BakimPerKontrolSarfiyat { get; set; }
        public DbSet<tb_BakimPlan> tb_BakimPlan { get; set; }
        public DbSet<tb_BakimPlanDetay> tb_BakimPlanDetay { get; set; }
        public DbSet<tb_KullaniciYetki> tb_KullaniciYetki { get; set; }
        public DbSet<tb_Proje> tb_Proje { get; set; }
        public DbSet<tb_Takvim> tb_Takvim { get; set; }
        public DbSet<tb_TakvimAyar> tb_TakvimAyar { get; set; }
        public DbSet<tb_Sayfa> tb_Sayfa { get; set; }
        public DbSet<tb_AiAyarlar> tb_AiAyarlar { get; set; }
        public DbSet<tb_Aygit> tb_Aygit { get; set; }
        public DbSet<tb_AygitKategori> tb_AygitKategori { get; set; }
        public DbSet<tb_Marka> tb_Marka { get; set; }
        public DbSet<tb_AygitPersonel> tb_AygitPersonel { get; set; }
        public DbSet<tb_Tedarikci> tb_Tedarikci { get; set; }
        public DbSet<tb_TedDeg> tb_TedDeg { get; set; }
        public DbSet<tb_TedDegKalitePuan> tb_TedDegKalitePuan { get; set; }
        public DbSet<tb_TedDegTurParam> tb_TedDegTurParam { get; set; }
        public DbSet<tb_TedDegParam> tb_TedDegParam { get; set; }
        public DbSet<tb_TedDegParamFormul> tb_TedDegParamFormul { get; set; }
        public DbSet<tb_TedDegTurSorumlu> tb_TedDegTurSorumlu { get; set; }
        public DbSet<viewAygitList> viewAygitList { get; set; }
        public DbSet<viewTedDegList> viewTedDegList { get; set; }
        public DbSet<tb_SayimAygit> tb_SayimAygit { get; set; }
        public DbSet<viewSayimAygit> viewSayimAygit { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Table Name Mappings
            modelBuilder.Entity<tb_Kullanici>().ToTable("tb_Kullanici").HasKey(e => e.KullaniciID);
            modelBuilder.Entity<tb_Ticket>().ToTable("tb_Ticket").HasKey(e => e.ID);
            modelBuilder.Entity<tb_TicketDosya>().ToTable("tb_TicketDosya").HasKey(e => e.ID);
            modelBuilder.Entity<tb_TicketAciklama>().ToTable("tb_TicketAciklama").HasKey(e => e.ID);
            modelBuilder.Entity<tb_TicketKategori>().ToTable("tb_TicketKategori").HasKey(e => e.ID);
            modelBuilder.Entity<tb_BelgeTarihce>().ToTable("tb_BelgeTarihce").HasKey(e => e.BelgeTarihceID);
            modelBuilder.Entity<tb_BelgeOnay>().ToTable("tb_BelgeOnay").HasKey(e => e.BelgeOnayID);
            modelBuilder.Entity<tb_Personel>().ToTable("tb_Personel").HasKey(e => e.SicilNo);

            modelBuilder.Entity<tb_Sirket>().ToTable("tb_Sirket").HasKey(e => e.SirketKodu);
            modelBuilder.Entity<tb_Bolum>().ToTable("tb_Bolum").HasKey(e => e.BolumKodu);
            modelBuilder.Entity<tb_Hat>().ToTable("tb_Hat").HasKey(e => e.HatKodu);
            modelBuilder.Entity<tb_Makine>().ToTable("tb_Makine").HasKey(e => e.MakineKodu);
            modelBuilder.Entity<tb_Malzeme>().ToTable("tb_Malzeme").HasKey(e => e.MalzemeKodu);
            modelBuilder.Entity<tb_IzinOnay>().ToTable("tb_IzinOnay").HasKey(e => e.IzinOnayID);
            modelBuilder.Entity<tb_Talep>().ToTable("tb_Talep").HasKey(e => e.TalepID);
            modelBuilder.Entity<tb_TalepBakim>().ToTable("tb_TalepBakim").HasKey(e => e.TalepKodu);
            modelBuilder.Entity<tb_TalepGelisme>().ToTable("tb_TalepGelisme").HasKey(e => e.TalepGelismeID);
            modelBuilder.Entity<tb_TalepKategori>().ToTable("tb_TalepKategori").HasKey(e => e.TalepKategoriID);
            modelBuilder.Entity<tb_TalepAmir>().ToTable("tb_TalepAmir").HasKey(e => e.TalepAmirID);
            modelBuilder.Entity<tb_TalepBilgi>().ToTable("tb_TalepBilgi").HasKey(e => e.TalepBilgiID);
            modelBuilder.Entity<tb_TalepSoruCevap>().ToTable("tb_TalepSoruCevap").HasKey(e => e.TalepSoruCevapID);
            modelBuilder.Entity<tb_TalepAyar>().ToTable("tb_TalepAyar").HasKey(e => e.TalepAyarID);
            modelBuilder.Entity<tb_Log>().ToTable("tb_Log").HasKey(e => e.LogID);
            modelBuilder.Entity<tb_Sms>().ToTable("tb_Sms").HasKey(e => e.SmsID);
            modelBuilder.Entity<tb_Hiyerarsi>().ToTable("tb_Hiyerarsi").HasKey(e => e.HiyerarsiID);
            modelBuilder.Entity<tb_Haber>().ToTable("tb_Haber").HasKey(e => e.HaberID);
            modelBuilder.Entity<tb_Egitim>().ToTable("tb_Egitim").HasKey(e => e.EgitimID);
            modelBuilder.Entity<tb_EgitimKategori>().ToTable("tb_EgitimKategori").HasKey(e => e.KategoriID);
            modelBuilder.Entity<tb_Departman>().ToTable("tb_Departman").HasKey(e => e.Kod);
            modelBuilder.Entity<tb_Unvan>().ToTable("tb_Unvan").HasKey(e => e.UnvanKodu);
            modelBuilder.Entity<SpBakimTalep>().HasNoKey().ToView(null);
            modelBuilder.Entity<tb_BakimPerKontrol>().ToTable("tb_BakimPerKontrol").HasKey(e => e.ID);
            modelBuilder.Entity<tb_BakimPerKontrolDetay>().ToTable("tb_BakimPerKontrolDetay").HasKey(e => e.ID);
            modelBuilder.Entity<tb_BakimPerKontrolSarfiyat>().ToTable("tb_BakimPerKontrolSarfiyat").HasKey(e => e.ID);
            modelBuilder.Entity<tb_BakimPlan>().ToTable("tb_BakimPlan").HasKey(e => e.ID);
            modelBuilder.Entity<tb_BakimPlanDetay>().ToTable("tb_BakimPlanDetay").HasKey(e => e.ID);
            modelBuilder.Entity<tb_KullaniciYetki>().ToTable("tb_KullaniciYetki").HasKey(e => e.KullaniciYetkiID);
            modelBuilder.Entity<tb_Proje>().ToTable("tb_Proje").HasKey(e => e.ProjeID);
            modelBuilder.Entity<tb_Sayfa>().ToTable("tb_Sayfa").HasKey(e => e.SayfaID);
            modelBuilder.Entity<tb_AiAyarlar>().ToTable("tb_AiAyarlar").HasKey(e => e.ID);
            modelBuilder.Entity<tb_Aygit>().ToTable("tb_Aygit").HasKey(e => e.AygitID);
            modelBuilder.Entity<tb_AygitKategori>().ToTable("tb_AygitKategori").HasKey(e => e.AygitKategoriID);
            modelBuilder.Entity<tb_Marka>().ToTable("tb_Marka").HasKey(e => e.MarkaID);
            modelBuilder.Entity<tb_AygitPersonel>().ToTable("tb_AygitPersonel").HasKey(e => e.AygitPersonelID);
            modelBuilder.Entity<tb_Tedarikci>().ToTable("tb_Tedarikci").HasKey(e => e.TedarikciKodu);
            modelBuilder.Entity<tb_TedDeg>().ToTable("tb_TedDeg").HasKey(e => e.TedDegID);
            modelBuilder.Entity<tb_TedDegKalitePuan>().ToTable("tb_TedDegKalitePuan").HasKey(e => new { e.BelgeNo, e.PKod });
            modelBuilder.Entity<tb_TedDegTurParam>().ToTable("tb_TedDegTurParam").HasKey(e => new { e.TurKod, e.PKod });
            modelBuilder.Entity<tb_TedDegParam>().ToTable("tb_TedDegParam").HasKey(e => e.PID);
            modelBuilder.Entity<tb_TedDegParamFormul>().ToTable("tb_TedDegParamFormul").HasKey(e => e.ID);
            modelBuilder.Entity<tb_TedDegTurSorumlu>().ToTable("tb_TedDegTurSorumlu").HasKey(e => e.ID);
            modelBuilder.Entity<viewAygitList>().ToView("viewAygitList").HasKey(e => e.AygitID);
            modelBuilder.Entity<viewTedDegList>().ToView("viewTedDegList").HasKey(e => e.TedDegID);
            modelBuilder.Entity<tb_SayimAygit>().ToTable("tb_SayimAygit").HasKey(e => e.AygitID);
            modelBuilder.Entity<viewSayimAygit>().ToView("ViewSayimAygit").HasKey(e => e.AygitID);
        }
    }
}
