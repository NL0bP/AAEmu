namespace AAEmu.Game.Models.Game.Char
{
    public class ExpertLimit
    {
        public uint Id { get; set; }
        public int UpLimit { get; set; }
        public byte ExpertLimitCount { get; set; }
        public int Advantage { get; set; }
        public int CastAdvantage { get; set; }
        //public uint UpCurrencyId { get; set; } // отсутствует в 0.5.101.406
        //public int UpPrice { get; set; } // отсутствует в 0.5.101.406
        //public uint DownCurrencyId { get; set; } // отсутствует в 0.5.101.406
        //public int DownPrice { get; set; } // отсутствует в 0.5.101.406
    }
}
