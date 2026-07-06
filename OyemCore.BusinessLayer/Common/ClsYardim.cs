using System;
using System.Collections.Generic;
using System.Linq;

namespace OyemCore.BusinessLayer.Common
{
    public class ClsYardim
    {
        public class ClassBakimSecenek
        {
            public string Tur { get; set; }
            public string Kod { get; set; }
            public string Tanim { get; set; }
            public int Deger { get; set; }
        }

        public static List<ClassBakimSecenek> TumListe()
        {
            return new List<ClassBakimSecenek>
            {
                new ClassBakimSecenek {Tur="ONEM", Deger=100,Kod="ACIL",Tanim="ACIL"},
                new ClassBakimSecenek {Tur="ONEM", Deger=1,Kod="AD",Tanim="ACIL DE??IL"},

                new ClassBakimSecenek {Tur="DURUS", Deger=10,Kod="EHAT",Tanim="HAT DURDU"},
                new ClassBakimSecenek {Tur="DURUS", Deger=5,Kod="EMAK",Tanim="MAKINE DURDU"},
                new ClassBakimSecenek {Tur="DURUS", Deger=1,Kod="H",Tanim="DURU?? YOK"},

                new ClassBakimSecenek {Tur="GIDA", Deger=10,Kod="Y",Tanim="Y?KSEK"},
                new ClassBakimSecenek {Tur="GIDA", Deger=5,Kod="O",Tanim="ORTA"},
                new ClassBakimSecenek {Tur="GIDA", Deger=1,Kod="D",Tanim="D????K"},

                new ClassBakimSecenek {Tur="ISG", Deger=10,Kod="Y",Tanim="Y?KSEK"},
                new ClassBakimSecenek {Tur="ISG", Deger=5,Kod="O",Tanim="ORTA"},
                new ClassBakimSecenek {Tur="ISG", Deger=1,Kod="D",Tanim="D????K"}
            };
        }

        public static string BakimPuanRenk(int? puan)
        {
            if (puan == null)
                return "";

            if (puan == 1) return "#90BE6D"; // Yesil
            if (puan <= 5) return "#277DA1"; // Mavi
            if (puan <= 25) return "#FFFA6F"; // Sari
            if (puan <= 50) return "#ff8f16"; // Turuncu
            if (puan <= 100) return "#db5d01"; // Koyu Turuncu
            return "#F94144"; // Kirmizi
        }
    }
}
