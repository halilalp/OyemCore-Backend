using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OyemCore.DataLayer.Entities;

namespace OyemCore.DataLayer.Interfaces
{
    public interface IYbsDbContext : IDisposable
    {
        DbSet<tb_Kullanici> tb_Kullanici { get; set; }
        DbSet<tb_Ticket> tb_Ticket { get; set; }
        DbSet<tb_TicketDosya> tb_TicketDosya { get; set; }
        DbSet<tb_TicketAciklama> tb_TicketAciklama { get; set; }
        DbSet<tb_TicketKategori> tb_TicketKategori { get; set; }
        DbSet<tb_BelgeTarihce> tb_BelgeTarihce { get; set; }
        DbSet<tb_BelgeOnay> tb_BelgeOnay { get; set; }
        DbSet<tb_Personel> tb_Personel { get; set; }

        DbSet<tb_Sirket> tb_Sirket { get; set; }
        DbSet<tb_Bolum> tb_Bolum { get; set; }
        DbSet<tb_Hat> tb_Hat { get; set; }
        DbSet<tb_Makine> tb_Makine { get; set; }
        DbSet<tb_Malzeme> tb_Malzeme { get; set; }
        DbSet<tb_IzinOnay> tb_IzinOnay { get; set; }
        DbSet<tb_Talep> tb_Talep { get; set; }
        DbSet<tb_TalepIsEmri> tb_TalepIsEmri { get; set; }
        DbSet<tb_SatOnay> tb_SatOnay { get; set; }
        DbSet<tb_SatKalem> tb_SatKalem { get; set; }
        DbSet<tb_SaSip> tb_SaSip { get; set; }
        DbSet<tb_SasKalem> tb_SasKalem { get; set; }
        DbSet<tb_Toplanti> tb_Toplanti { get; set; }
        DbSet<tb_ToplantiGorev> tb_ToplantiGorev { get; set; }
        DbSet<tb_ToplantiKullanici> tb_ToplantiKullanici { get; set; }
        DbSet<tb_ToplantiDosya> tb_ToplantiDosya { get; set; }
        DbSet<tb_RecProjeTur> tb_RecProjeTur { get; set; }
        DbSet<tb_IsEmriTur> tb_IsEmriTur { get; set; }
        DbSet<tb_TalepBakim> tb_TalepBakim { get; set; }
        DbSet<tb_TalepGelisme> tb_TalepGelisme { get; set; }
        DbSet<tb_TalepKategori> tb_TalepKategori { get; set; }
        DbSet<tb_TalepAmir> tb_TalepAmir { get; set; }
        DbSet<tb_TalepBilgi> tb_TalepBilgi { get; set; }
        DbSet<tb_TalepSoruCevap> tb_TalepSoruCevap { get; set; }
        DbSet<tb_TalepAyar> tb_TalepAyar { get; set; }
        DbSet<tb_Log> tb_Log { get; set; }
        DbSet<tb_Sms> tb_Sms { get; set; }
        DbSet<tb_Hiyerarsi> tb_Hiyerarsi { get; set; }
        DbSet<tb_Haber> tb_Haber { get; set; }
        DbSet<tb_Egitim> tb_Egitim { get; set; }
        DbSet<tb_EgitimKategori> tb_EgitimKategori { get; set; }
        DbSet<tb_Departman> tb_Departman { get; set; }
        DbSet<tb_Unvan> tb_Unvan { get; set; }
        DbSet<SpBakimTalep> SpBakimTalep { get; set; }
        DbSet<tb_BakimPerKontrol> tb_BakimPerKontrol { get; set; }
        DbSet<tb_BakimPerKontrolDetay> tb_BakimPerKontrolDetay { get; set; }
        DbSet<tb_BakimPerKontrolSarfiyat> tb_BakimPerKontrolSarfiyat { get; set; }
        DbSet<tb_BakimSarfiyat> tb_BakimSarfiyat { get; set; }
        DbSet<tb_TalepSureHesap> tb_TalepSureHesap { get; set; }
        DbSet<tb_BakimPlan> tb_BakimPlan { get; set; }
        DbSet<tb_BakimPlanDetay> tb_BakimPlanDetay { get; set; }
        DbSet<tb_KullaniciYetki> tb_KullaniciYetki { get; set; }
        DbSet<tb_Proje> tb_Proje { get; set; }
        DbSet<tb_Takvim> tb_Takvim { get; set; }
        DbSet<tb_TakvimAyar> tb_TakvimAyar { get; set; }
        DbSet<tb_Sayfa> tb_Sayfa { get; set; }
        DbSet<tb_AiAyarlar> tb_AiAyarlar { get; set; }
        DbSet<tb_Aygit> tb_Aygit { get; set; }
        DbSet<tb_AygitKategori> tb_AygitKategori { get; set; }
        DbSet<tb_Marka> tb_Marka { get; set; }
        DbSet<tb_AygitPersonel> tb_AygitPersonel { get; set; }
        DbSet<tb_BakimPlanTemizlikOnay> tb_BakimPlanTemizlikOnay { get; set; }
        DbSet<tb_Tedarikci> tb_Tedarikci { get; set; }
        DbSet<tb_TedDeg> tb_TedDeg { get; set; }
        DbSet<tb_TedDegKalitePuan> tb_TedDegKalitePuan { get; set; }
        DbSet<tb_TedDegTurParam> tb_TedDegTurParam { get; set; }
        DbSet<tb_TedDegParam> tb_TedDegParam { get; set; }
        DbSet<tb_TedDegParamFormul> tb_TedDegParamFormul { get; set; }
        DbSet<tb_TedDegTurSorumlu> tb_TedDegTurSorumlu { get; set; }
        DbSet<viewAygitList> viewAygitList { get; set; }
        DbSet<viewTedDegList> viewTedDegList { get; set; }
        DbSet<tb_SayimAygit> tb_SayimAygit { get; set; }
        DbSet<viewSayimAygit> viewSayimAygit { get; set; }
        DbSet<tb_UserDevices> tb_UserDevices { get; set; }
        DbSet<tb_AkademiEgitim> tb_AkademiEgitim { get; set; }
        DbSet<tb_AkademiAtama> tb_AkademiAtama { get; set; }
        DbSet<tb_AkademiIlerleme> tb_AkademiIlerleme { get; set; }
        DbSet<tb_AkademiKategori> tb_AkademiKategori { get; set; }
        DbSet<tb_AkademiSoru> tb_AkademiSoru { get; set; }
        DbSet<tb_AkademiSinavOturum> tb_AkademiSinavOturum { get; set; }
        DbSet<tb_AkademiSinavSonuc> tb_AkademiSinavSonuc { get; set; }
        DbSet<tb_Notification> tb_Notification { get; set; }
        DbSet<tb_Chat> tb_Chat { get; set; }
        DbSet<tb_ChatGroup> tb_ChatGroup { get; set; }
        DbSet<tb_ChatGroupMember> tb_ChatGroupMember { get; set; }
        DbSet<tb_GameScore> tb_GameScore { get; set; }
        DbSet<tb_GameWord> tb_GameWord { get; set; }
        DbSet<tb_GameWordPool> tb_GameWordPool { get; set; }
        DbSet<tb_Anket> tb_Anket { get; set; }
        DbSet<tb_AnketSoru> tb_AnketSoru { get; set; }
        DbSet<tb_AnketSoruSecenek> tb_AnketSoruSecenek { get; set; }
        DbSet<tb_AnketSonuc> tb_AnketSonuc { get; set; }
        DbSet<tb_AnketPersonel> tb_AnketPersonel { get; set; }
        DbSet<tb_MagazaAvans> tb_MagazaAvans { get; set; }
        DbSet<tb_MagazaMasraf> tb_MagazaMasraf { get; set; }
        DbSet<tb_MagazaMasrafDetay> tb_MagazaMasrafDetay { get; set; }
        DbSet<tb_Mail> tb_Mail { get; set; }
        DbSet<tb_Ayarlar> tb_Ayarlar { get; set; }
        // Malzeme & Stok yonetimi (mobil gecis)
        DbSet<tb_MalzemeGrubu> tb_MalzemeGrubu { get; set; }
        DbSet<tb_Birim> tb_Birim { get; set; }
        DbSet<tb_MalzemeTip> tb_MalzemeTip { get; set; }
        DbSet<tb_MalzemeTedarikciKodu> tb_MalzemeTedarikciKodu { get; set; }
        DbSet<tb_MalzemeOzellikTanim> tb_MalzemeOzellikTanim { get; set; }
        DbSet<tb_MalzemeHareket> tb_MalzemeHareket { get; set; }
        // Stok yonetimi (mobil gecis - Faz 2)
        DbSet<tb_Depo> tb_Depo { get; set; }
        DbSet<tb_MalzemeHareketTip> tb_MalzemeHareketTip { get; set; }
        DbSet<tb_MalzemeFis> tb_MalzemeFis { get; set; }
        DbSet<tb_DepoMalzeme> tb_DepoMalzeme { get; set; }
        DbSet<tb_MalzemeLot> tb_MalzemeLot { get; set; }
        DbSet<tb_MalzemeLotAnaliz> tb_MalzemeLotAnaliz { get; set; }
        DbSet<tb_DepoSorumlusu> tb_DepoSorumlusu { get; set; }
        DbSet<tb_SistemAyarlari> tb_SistemAyarlari { get; set; }
        DbSet<tb_Entegrasyonlar> tb_Entegrasyonlar { get; set; }
        DbSet<tb_MagazaSatisParametre> tb_MagazaSatisParametre { get; set; }
        DbSet<tb_MagazaSatisParametreDeger> tb_MagazaSatisParametreDeger { get; set; }
        DbSet<tb_Koleksiyon> tb_Koleksiyon { get; set; }
        DbSet<tb_MalzemeOzellikDeger> tb_MalzemeOzellikDeger { get; set; }
        DbSet<tb_MalzemeOzellik> tb_MalzemeOzellik { get; set; }
        DbSet<tb_MalzemeSKU> tb_MalzemeSKU { get; set; }

        DatabaseFacade Database { get; }

        int SaveChanges();
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
