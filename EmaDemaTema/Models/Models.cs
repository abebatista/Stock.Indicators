using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Skender.Stock.Indicators;

namespace EmaDemaTema.Models
{
    public class DayQuote : Quote
    {
        /*
        public DateTime Date { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
        */
        public string Symbol { get; set; }
    }

    public class JsonObjectResultBase
    {
        public string result { get; set; }
        public string description { get; set; }
    }

    public class JsonObjectResult : JsonObjectResultBase
    {
        public List<DayQuote> listDayQuote { get; set; }
    }

    public class JsonObjectResultStats : JsonObjectResultBase
    {
        public int total_trades { get; set; }
        public int total_trades_won { get; set; }

        public int total_trades_lost { get; set; }

        public decimal total_trades_won_percentage { get; set; }

        public decimal total_trades_lost_percentage { get; set; }

        public decimal funds { get; set; }

        public int ma_length { get; set; }
    }

    public class JsonObjectResultStatsTotal : JsonObjectResultBase
    {
        public JsonObjectResultStats combined_results { get; set; }
        public List<JsonObjectResultStats> JsonObjectResultStatsRecords { get; set; }
        
    }

}
