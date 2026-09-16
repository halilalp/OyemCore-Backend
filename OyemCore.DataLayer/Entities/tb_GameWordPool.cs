using System;

namespace OyemCore.DataLayer.Entities
{
    // Kelime havuzu (hedef kelimeler). Word ve Meaning şifreli saklanır. referans: webportal tb_GameWordPool
    public class tb_GameWordPool
    {
        public int Id { get; set; }
        public string Word { get; set; }        // şifreli (ClsEncryption)
        public string Meaning { get; set; }     // şifreli (ClsEncryption)
        public int WordLength { get; set; }
        public bool IsActive { get; set; }
    }
}
