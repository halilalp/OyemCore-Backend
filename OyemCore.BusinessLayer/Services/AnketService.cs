using System;
using System.Collections.Generic;
using System.Linq;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    // referans: WebServiceAnket (oylama alt kümesi) — .NET 7 / EF Core portu.
    public class AnketService : IAnketService
    {
        private readonly IYbsDbContext _context;

        public AnketService(IYbsDbContext context)
        {
            _context = context;
        }

        private string GetSicilNo(int kullaniciID)
            => _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID)?.SicilNo;

        // ── Aktif + katılınmamış anketler (ana ekran) ──
        // referans: DashboardAnketleriGetir
        public IEnumerable<object> GetActiveSurveys(int kullaniciID)
        {
            string sicil = GetSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(sicil)) return new List<object>();
            sicil = sicil.Trim();
            DateTime now = DateTime.Now;

            var active = _context.tb_Anket
                .Where(a => a.Durum == true && a.BasTar <= now && a.BitisTar >= now)
                .OrderByDescending(a => a.BasTar)
                .ToList();

            var votedAnketIds = _context.tb_AnketPersonel
                .Where(p => p.SicilNo == sicil)
                .Select(p => p.AnketID)
                .ToList();

            return active
                .Where(a => !votedAnketIds.Contains(a.AnketID))
                .Select(a => (object)new
                {
                    a.AnketID,
                    a.Konu,
                    a.Aciklama,
                    BitisTar = a.BitisTar.HasValue ? a.BitisTar.Value.ToString("dd.MM.yyyy") : "",
                    a.RefUrl
                }).ToList();
        }

        // ── Oylama formu (sorular + seçenekler) ──
        public object GetSurveyDetail(int kullaniciID, int anketID)
        {
            string sicil = GetSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(sicil)) return new { error = true, message = "Kullanıcı bulunamadı." };
            sicil = sicil.Trim();

            var ank = _context.tb_Anket.FirstOrDefault(a => a.AnketID == anketID);
            if (ank == null) return new { error = true, message = "Anket bulunamadı." };

            DateTime now = DateTime.Now;
            bool active = (ank.Durum == true) && (!ank.BasTar.HasValue || ank.BasTar <= now) && (!ank.BitisTar.HasValue || ank.BitisTar >= now);
            bool alreadyVoted = _context.tb_AnketPersonel.Any(p => p.AnketID == anketID && p.SicilNo == sicil);

            var sorular = _context.tb_AnketSoru
                .Where(s => s.AnketID == anketID)
                .OrderBy(s => s.AnketSoruID)
                .ToList();

            var soruIds = sorular.Select(s => s.AnketSoruID).ToList();
            var secenekler = _context.tb_AnketSoruSecenek
                .Where(x => x.SoruID != null && soruIds.Contains(x.SoruID.Value))
                .OrderBy(x => x.AnketSecenekID)
                .ToList();

            var sorularDto = sorular.Select(s => new
            {
                s.AnketSoruID,
                s.Soru,
                s.Aciklama,
                Tur = s.Tur ?? "SEC",
                Secenekler = secenekler.Where(x => x.SoruID == s.AnketSoruID)
                    .Select(x => new { x.AnketSecenekID, x.Secenek }).ToList()
            }).ToList();

            return new
            {
                success = true,
                ank.AnketID,
                ank.Konu,
                ank.Aciklama,
                BitisTar = ank.BitisTar.HasValue ? ank.BitisTar.Value.ToString("dd.MM.yyyy") : "",
                ank.RefUrl,
                active,
                alreadyVoted,
                Sorular = sorularDto
            };
        }

        // ── Oy gönder ──
        // referans: AnketKaydet (tb_Personel/doğum tarihi legacy kontrolü mobilde atlanır; JWT ile SicilNo doğrulanmış).
        public object SubmitVote(int kullaniciID, int anketID, List<AnketAnswer> answers)
        {
            string sicil = GetSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(sicil)) return new { error = true, message = "Kullanıcı bulunamadı." };
            sicil = sicil.Trim();

            var ank = _context.tb_Anket.FirstOrDefault(a => a.AnketID == anketID);
            if (ank == null) return new { error = true, message = "Aradığınız anket bulunamadı." };

            if (ank.Durum == false || (ank.BitisTar.HasValue && DateTime.Now > ank.BitisTar.Value))
                return new { error = true, message = "Anket tamamlandığından katılamazsınız." };
            if (ank.BasTar.HasValue && DateTime.Now < ank.BasTar.Value)
                return new { error = true, message = "Anket henüz başlamadı." };

            if (_context.tb_AnketPersonel.Any(p => p.AnketID == anketID && p.SicilNo == sicil))
                return new { error = true, message = "Ankete sadece 1 kez katılabilirsiniz." };

            var sorular = _context.tb_AnketSoru.Where(s => s.AnketID == anketID).ToList();
            answers = answers ?? new List<AnketAnswer>();

            // Tüm sorular cevaplanmalı.
            if (sorular.Count != answers.Count(a => a != null))
                return new { error = true, message = "Tüm anket sorularını cevaplamanız gerekmektedir." };

            DateTime nowT = DateTime.Now;
            foreach (var ans in answers)
            {
                if (ans == null) continue;
                var soru = sorular.FirstOrDefault(s => s.AnketSoruID == ans.SoruID);
                if (soru == null) return new { error = true, message = "Geçersiz soru." };

                var sonuc = new tb_AnketSonuc
                {
                    AnketID = anketID,
                    SoruID = soru.AnketSoruID,
                    SicilNo = sicil,
                    KayitTar = nowT
                };

                if ((soru.Tur ?? "SEC") == "SEC")
                {
                    var sec = _context.tb_AnketSoruSecenek.FirstOrDefault(x => x.AnketSecenekID == ans.SecenekID);
                    if (sec == null) return new { error = true, message = "Lütfen tüm sorular için bir seçenek işaretleyin." };
                    sonuc.SecilenSecenekID = sec.AnketSecenekID;
                    sonuc.Puan = sec.Puan ?? 0;
                    sonuc.Cevap = sec.Secenek;
                }
                else
                {
                    sonuc.Cevap = ans.Cevap ?? "";
                    sonuc.Puan = 0;
                }
                _context.tb_AnketSonuc.Add(sonuc);
            }

            _context.tb_AnketPersonel.Add(new tb_AnketPersonel
            {
                AnketID = anketID,
                SicilNo = sicil,
                IP = "mobile",
                KayitTar = nowT
            });
            _context.SaveChanges();

            return new { success = true, message = "Ankete katıldığınız için teşekkür ederiz." };
        }
    }
}
