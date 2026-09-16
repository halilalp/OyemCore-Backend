using System;

namespace OyemCore.DataLayer.Entities
{
    // Aktif sinav oturumu — tek-oturum kilidi (Atama basina en fazla bir tamamlanmamis
    // oturum), sunucu-yetkili soru sirasi/secenek karistirma ve soru-basi sure ankoru.
    public class tb_AkademiSinavOturum
    {
        public int OturumID { get; set; }
        public int AtamaID { get; set; }
        public string SoruSiraJson { get; set; } = null!; // [{soruId, secenekSira:["A","C","B","D"]}]
        public int MevcutSoruIndex { get; set; }
        public DateTime BaslangicZamani { get; set; }
        public DateTime SoruBaslangicZamani { get; set; } // mevcut sorunun sunucu-yetkili baslangic zamani
        public int SekmeDegisimSayisi { get; set; }
        public bool TamamlandiMi { get; set; }
        public string CevaplarJson { get; set; } = null!; // [{soruId, secilenSecenek, doğruMu, sureAsimiMi}]
    }
}
