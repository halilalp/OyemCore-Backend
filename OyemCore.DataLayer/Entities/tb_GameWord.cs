using System;

namespace OyemCore.DataLayer.Entities
{
    // Günün kelime oyunu tanımı (o güne kaç oyun + kelime uzunluğu). referans: webportal tb_GameWord
    public class tb_GameWord
    {
        public int Id { get; set; }
        public DateTime GameDate { get; set; }     // date (gün)
        public int? WordLength { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
