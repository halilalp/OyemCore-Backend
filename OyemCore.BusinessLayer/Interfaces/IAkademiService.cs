using System;
using System.Collections.Generic;

namespace OyemCore.BusinessLayer.Interfaces
{
    // Akademi — Faz 1: içerik yönetimi, atama, gerçek izleme takibi.
    // tb_Egitim/tb_EgitimKategori'ye (mevcut "Eğitimler" kaynak havuzu) kasıtlı olarak dokunmuyor.
    public interface IAkademiService
    {
        // İçerik (İK yönetimi)
        IEnumerable<object> GetContentList(int kullaniciID);
        bool SaveContent(int kullaniciID, string baslik, string aciklama, string kategoriKodu, string icerikTipi, string dosyaUrl, int? sureSaniye);
        bool UpdateContent(int kullaniciID, int akademiEgitimID, string baslik, string aciklama, string kategoriKodu, string dosyaUrl, int? sureSaniye);
        bool SetContentActive(int kullaniciID, int akademiEgitimID, bool aktifMi);

        // Atama (İK yönetimi)
        int AssignToPersonnel(int kullaniciID, int akademiEgitimID, List<string> sicilNoList, DateTime? sonTarih, bool zorunluMu, bool aktifIzlemeZorunlu);

        // Personel tarafı (web + mobil ortak)
        IEnumerable<object> GetMyAssignments(string sicilNo);
        object GetAssignmentDetail(int atamaID, string sicilNo);
        bool UpdateProgress(int atamaID, string sicilNo, int maxIzlenenSaniye, int aktifIzlemeSaniyeArtis, bool tamamlaZorla = false);

        // İK raporlama
        IEnumerable<object> GetAssignmentReport(int akademiEgitimID);

        // -------------------------------------------------------------
        // Faz 2 — Sınav motoru
        // -------------------------------------------------------------

        // Kategori (DB'den, serbest metin yerine)
        IEnumerable<object> GetCategories();
        bool AddCategory(int kullaniciID, string ad);

        // Sınav ayarları (içeriğe bağlı, parametrik)
        bool SaveExamSettings(int kullaniciID, int akademiEgitimID, bool sinavAktif, int? soruSayisi, int soruSuresiSaniye, int gecmePuanYuzdesi);

        // Soru bankası (İK yönetimi)
        IEnumerable<object> GetQuestions(int akademiEgitimID);
        bool SaveQuestion(int kullaniciID, int akademiEgitimID, string soruMetni, string secenekA, string secenekB, string? secenekC, string? secenekD, string dogruSecenek, int puan);
        bool UpdateQuestion(int kullaniciID, int soruID, string soruMetni, string secenekA, string secenekB, string? secenekC, string? secenekD, string dogruSecenek, int puan);
        bool DeleteQuestion(int kullaniciID, int soruID);
        int ImportQuestionsBulk(int kullaniciID, int akademiEgitimID, List<(string soruMetni, string a, string b, string? c, string? d, string dogru, int puan)> rows);

        // Sınav oturumu (personel tarafı — web + mobil ortak, tek-oturum kilidi)
        object GetExamBrief(int atamaID, string sicilNo);
        object StartOrResumeExam(int atamaID, string sicilNo);
        object SubmitAnswer(int atamaID, string sicilNo, string secilenSecenek);
        bool ReportTabSwitch(int atamaID, string sicilNo);
        object? GetExamResult(int atamaID, string sicilNo);
    }
}
