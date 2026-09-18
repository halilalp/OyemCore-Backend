using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OyemCore.BusinessLayer.Common
{
    // tb_Kullanici.AdminBelgeTur uzerindeki TEK merkezi parse/kontrol noktasi. Bundan once bu
    // kontrol ~15 dosyada kendi ozel (ve cogu anchor'siz/gevsek) ".Contains(...)" bloguyla
    // tekrarlaniyordu — biri "BAKIMADMIN" sahibini otomatik super-admin sayiyordu, digeri hic
    // whitelist kontrolu yapmiyordu. WebPortal'daki DataLayer/ClsYetki.cs ile AYNI davranisi
    // (JSON-oncelikli parse, "*KOD*" eski formata geriye-donuk uyum, EXACT match, SADECE "ADMIN"
    // super-admin bypass'i) yeniden uygular — iki backend tutarli calismali.
    public static class AdminBelgeTuruHelper
    {
        public static List<string> Parse(string adminBelgeTur)
        {
            if (string.IsNullOrWhiteSpace(adminBelgeTur)) return new List<string>();
            string trimmed = adminBelgeTur.Trim();

            if (trimmed.StartsWith("["))
            {
                try
                {
                    var kodlar = JsonSerializer.Deserialize<List<string>>(trimmed);
                    if (kodlar != null)
                    {
                        return kodlar.Where(o => !string.IsNullOrWhiteSpace(o))
                                     .Select(o => o.Trim().ToUpperInvariant())
                                     .ToList();
                    }
                }
                catch
                {
                    // Bozuk JSON — guvenli tarafta kal, hic yetki verme.
                    return new List<string>();
                }
            }

            // Eski format: "*KOD1*KOD2*"
            return trimmed.Split('*', StringSplitOptions.RemoveEmptyEntries)
                          .Select(o => o.Trim().ToUpperInvariant())
                          .ToList();
        }

        // Exact-match kesin yetki kontrolu. "ADMIN" (sistem yoneticisi) her zaman kapsanir.
        public static bool HasYetki(string adminBelgeTur, string kod)
        {
            if (string.IsNullOrWhiteSpace(kod)) return false;
            var kodlar = Parse(adminBelgeTur);
            return kodlar.Contains("ADMIN") || kodlar.Contains(kod.Trim().ToUpperInvariant());
        }

        public static bool HasAnyYetki(string adminBelgeTur, params string[] kodlar)
        {
            if (kodlar == null || kodlar.Length == 0) return false;
            var yetkiler = Parse(adminBelgeTur);
            if (yetkiler.Contains("ADMIN")) return true;
            return kodlar.Any(k => !string.IsNullOrWhiteSpace(k) && yetkiler.Contains(k.Trim().ToUpperInvariant()));
        }

        public static bool IsAdmin(string adminBelgeTur) => Parse(adminBelgeTur).Contains("ADMIN");

        public static string Serialize(IEnumerable<string> kodlar)
        {
            var temiz = (kodlar ?? Enumerable.Empty<string>())
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Select(o => o.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();
            return JsonSerializer.Serialize(temiz);
        }
    }
}
