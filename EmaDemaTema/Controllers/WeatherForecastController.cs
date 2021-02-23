using EmaDemaTema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Skender.Stock.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YahooFinanceAPI;

namespace EmaDemaTema.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IEnumerable<WeatherForecast> Get()
        {
            var rng = new Random();
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)]
            })
            .ToArray();
        }

        protected async Task<JsonObjectResult> processGetHistoricalDataViaYahooFinanceAPI(string symbol = "SPY", int fromYear = 1900, int fromMonth = 1, int fromDay = 1, int toYear = 0, int toMonth = 0, int toDay = 0)
        {
            JsonObjectResult _JsonObjectResult = new JsonObjectResult()
            {
                result = "OK",
                description = "OK",
                listDayQuote = new List<DayQuote>()
            };
            try
            {
                //https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol=BAC&outputsize=full&apikey=FYI0YOP1FZ7LJ7LU
                //https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol=BAC&outputsize=full&apikey=FYI0YOP1FZ7LJ7LU&datatype=csv
                var dateFrom = new DateTime(fromYear, fromMonth, fromDay);
                var dateTo = DateTime.Now;
                if (toYear != 0)
                {
                    dateTo = new DateTime(toYear, toMonth, toDay);
                }
                //first get a valid token from Yahoo Finance
                while (string.IsNullOrEmpty(Token.Cookie) || string.IsNullOrEmpty(Token.Crumb))
                {
                    await Token.RefreshAsync();
                }
                var newBars = await Historical.GetPriceAsync(symbol, dateFrom, dateTo);
                var dateDictionary = new Dictionary<string, string>();
                var l = newBars.Count;
                for (var i = 0; i < l; i++)
                {
                    var barSet_symbol_record = newBars[i];
                    var date = barSet_symbol_record.Date.ToString("yyyy-MM-dd HH:mm:ss");
                    if (dateDictionary.ContainsKey(date))
                    {
                        continue;
                    }
                    if (barSet_symbol_record.Close <= 0 || barSet_symbol_record.High <= 0 || barSet_symbol_record.Low <= 0 || barSet_symbol_record.Open <= 0 || barSet_symbol_record.Volume <= 0)
                    {
                        continue;
                    }
                    dateDictionary.Add(date, date);
                    _JsonObjectResult.listDayQuote.Add(new DayQuote
                    {
                        Close = Convert.ToDecimal(barSet_symbol_record.Close),
                        Open = Convert.ToDecimal(barSet_symbol_record.Open),
                        High = Convert.ToDecimal(barSet_symbol_record.High),
                        Low = Convert.ToDecimal(barSet_symbol_record.Low),
                        Volume = Convert.ToDecimal(barSet_symbol_record.Volume),
                        Date = barSet_symbol_record.Date,
                        Symbol = symbol
                    });
                }
                _JsonObjectResult.listDayQuote = _JsonObjectResult.listDayQuote.OrderBy(x => x.Date).ToList();
            }
            catch (Exception ex)
            {
                _JsonObjectResult.result = "ERROR";
                _JsonObjectResult.description = ex.Message;
            }
            return _JsonObjectResult;
        }

        [HttpGet, HttpPost]
        [Route("GetHistoricalDataViaYahooFinanceAPI")]
        public async Task<JsonObjectResult> GetHistoricalDataViaYahooFinanceAPI(string symbol = "SPY", int fromYear = 1900, int fromMonth = 1, int fromDay = 1, int toYear = 0, int toMonth = 0, int toDay = 0)
        {
            JsonObjectResult _JsonObjectResult = new JsonObjectResult()
            {
                result = "OK",
                description = "OK",
                listDayQuote = new List<DayQuote>()
            };
            try
            {
                _JsonObjectResult = await processGetHistoricalDataViaYahooFinanceAPI(symbol, fromYear, fromMonth, fromDay, toYear, toMonth, toDay);
            }
            catch (Exception ex)
            {
                _JsonObjectResult.result = "ERROR";
                _JsonObjectResult.description = ex.Message;
            }
            return _JsonObjectResult;
        }

        protected async Task<JsonObjectResultStats> processGetEmaDemaTema(List<DayQuote> df, int ma_length = 14, decimal funds = 100000)
        {
            JsonObjectResultStats _JsonObjectResult = new JsonObjectResultStats()
            {
                result = "OK",
                description = "OK"
            };
            try
            {

                List<EmaResult> df_ema = Indicator.GetEma(df, ma_length).ToList();
                List<EmaResult> df_dema = Indicator.GetDoubleEma(df, ma_length).ToList();
                List<EmaResult> df_tema = Indicator.GetTripleEma(df, ma_length).ToList();

                var l = df.Count;

                var current_df = new DayQuote();
                var current_df_ema = new EmaResult();
                var current_df_dema = new EmaResult();
                var current_df_tema = new EmaResult();

                var previous_df = new DayQuote();
                var previous_df_ema = new EmaResult();
                var previous_df_dema = new EmaResult();
                var previous_df_tema = new EmaResult();

                int in_position = 0;
                int shares = 1;

                int total_trades = 0;
                int total_trades_won = 0;
                int total_trades_lost = 0;

                decimal position_entry_price = 0;
                decimal position_exit_price = 0;

                for (var i = ma_length; i < l; i++)
                {

                    current_df = df[i];
                    current_df_ema = df_ema[i];
                    current_df_dema = df_dema[i];
                    current_df_tema = df_tema[i];

                    previous_df = df[i - 1];
                    previous_df_ema = df_ema[i - 1];
                    previous_df_dema = df_dema[i - 1];
                    previous_df_tema = df_tema[i - 1];

                    if (in_position == 0)
                    {
                        int shares_temp = Convert.ToInt32((funds / current_df.Close).ToString().Split('.')[0]);
                        if ((current_df_tema.Ema > current_df_dema.Ema) && (current_df_dema.Ema > current_df_ema.Ema) && (previous_df_tema.Ema < previous_df_dema.Ema) && (previous_df_dema.Ema < previous_df_ema.Ema) && (shares_temp > 0))
                        {
                            position_entry_price = current_df.Close;
                            in_position = 1;
                            total_trades = total_trades + 1;
                            shares = shares_temp;
                            funds = funds - (position_entry_price * shares);
                        }
                        else
                        {
                            in_position = 0;
                        }
                    }
                    else
                    {
                        if ((current_df_tema.Ema < current_df_dema.Ema) && (current_df_dema.Ema < current_df_ema.Ema) && (previous_df_tema.Ema > previous_df_dema.Ema) && (previous_df_dema.Ema > previous_df_ema.Ema))
                        {
                            position_exit_price = current_df.Close;
                            in_position = 0;
                            if (position_entry_price <= position_exit_price)
                            {
                                total_trades_won = total_trades_won + 1;
                                funds = funds + (position_exit_price * shares);
                            }
                            else
                            {
                                total_trades_lost = total_trades_lost + 1;
                                //funds = funds - ((position_entry_price - position_exit_price) * shares);
                                funds = funds + (position_exit_price * shares);
                            }
                        }
                        else
                        {
                            in_position = 1;
                        }
                    }
                }

                if (in_position == 1)
                {
                    position_exit_price = current_df.Close;
                    in_position = 0;
                    if (position_entry_price <= position_exit_price)
                    {
                        total_trades_won = total_trades_won + 1;
                        funds = funds + ((position_exit_price - position_entry_price) * shares);
                    }
                    else
                    {
                        total_trades_lost = total_trades_lost + 1;
                        funds = funds - ((position_entry_price - position_exit_price) * shares);
                    }
                }

                int total_trades_won_percentage = 0;
                if (total_trades_won > 0)
                    total_trades_won_percentage = (total_trades_won * 100) / total_trades;
                int total_trades_lost_percentage = 0;
                if (total_trades_lost > 0)
                    total_trades_lost_percentage = (total_trades_lost * 100) / total_trades;

                _JsonObjectResult.total_trades = total_trades;
                _JsonObjectResult.total_trades_won = total_trades_won;
                _JsonObjectResult.total_trades_lost = total_trades_lost;
                _JsonObjectResult.total_trades_won_percentage = total_trades_won_percentage;
                _JsonObjectResult.total_trades_lost_percentage = total_trades_lost_percentage;
                _JsonObjectResult.funds = funds;
                _JsonObjectResult.ma_length = ma_length;

            }
            catch (Exception ex)
            {
                _JsonObjectResult.result = "ERROR";
                _JsonObjectResult.description = ex.Message;
            }
            return _JsonObjectResult;
        }


        [HttpGet, HttpPost]
        [Route("GetEmaDemaTema")]
        public async Task<JsonObjectResultStatsTotal> GetEmaDemaTema(string symbols = "SPY", int fromYear = 1900, int fromMonth = 1, int fromDay = 1, int toYear = 0, int toMonth = 0, int toDay = 0
            , int funds = 10000, int init_row = 3, int max_row = 51)
        {
            JsonObjectResultStatsTotal _JsonObjectResult = new JsonObjectResultStatsTotal()
            {
                result = "OK",
                description = "OK",
                JsonObjectResultStatsRecords = new List<JsonObjectResultStats>()
            };
            try
            {
                var equals = false;
                var old_char = ("'").ToCharArray()[0];
                var new_char = (" ").ToCharArray()[0];
                var new_symbols = "";
                while (!equals)
                {
                    new_symbols = symbols.Replace(old_char, new_char);
                    equals = new_symbols == symbols;
                    symbols = new_symbols;
                }
                var symbolList = symbols.Split(',').ToList();
                var symbolHistoricalList = new Dictionary<string, JsonObjectResult>();
                var results_final = new Dictionary<string, List<JsonObjectResultStats>>();
                foreach (var new_symbol in symbolList)
                {
                    var symbol = new_symbol.Trim();
                    var _JsonObjectResultHistorical = await processGetHistoricalDataViaYahooFinanceAPI(symbol, fromYear, fromMonth, fromDay, toYear, toMonth, toDay);
                    symbolHistoricalList.Add(symbol, _JsonObjectResultHistorical);
                    var _processGetEmaDemaTemaList = new List<JsonObjectResultStats>();
                    for (var ii = init_row; ii < max_row; ii++)
                    {
                        var _processGetEmaDemaTema = await processGetEmaDemaTema(_JsonObjectResultHistorical.listDayQuote, ii, funds);
                        _processGetEmaDemaTemaList.Add(_processGetEmaDemaTema);
                    }
                    results_final.Add(symbol, _processGetEmaDemaTemaList);
                }

                var counter_stocks = 0;
                var stats = new List<JsonObjectResultStats>();
                var combined_results = new JsonObjectResultStats();
                foreach (var new_symbol in symbolList)
                {
                    var symbol = new_symbol.Trim();
                    var results_final_record_stats = results_final.ElementAt(counter_stocks).Value;
                    counter_stocks = counter_stocks + 1;
                    if (counter_stocks == 1)
                    {
                        stats = results_final_record_stats;
                        continue;
                    }
                    var counter_stats = 0;
                    foreach (var results_final_record_stats_current in results_final_record_stats)
                    {
                        var stats_current = stats[counter_stats];

                        stats_current.funds = (stats_current.funds + results_final_record_stats_current.funds) / 2;
                        stats_current.total_trades = (stats_current.total_trades + results_final_record_stats_current.total_trades);
                        stats_current.total_trades_lost = (stats_current.total_trades_lost + results_final_record_stats_current.total_trades_lost);
                        stats_current.total_trades_won = (stats_current.total_trades_won + results_final_record_stats_current.total_trades_won);

                        if (stats_current.total_trades > 0)
                        {
                            stats_current.total_trades_won_percentage = (stats_current.total_trades_won * 100) / stats_current.total_trades;
                            stats_current.total_trades_lost_percentage = (stats_current.total_trades_lost * 100) / stats_current.total_trades;
                        }

                        stats[counter_stats] = stats_current;
                        /*
                        if (results_final_record_stats_current.total_trades > 0)
                        {
                            combined_results.total_trades = (combined_results.total_trades + results_final_record_stats_current.total_trades);
                            combined_results.total_trades_lost = (combined_results.total_trades_lost + results_final_record_stats_current.total_trades_lost);
                            combined_results.total_trades_won = (combined_results.total_trades_won + results_final_record_stats_current.total_trades_won);
                            combined_results.total_trades_won_percentage = (combined_results.total_trades_won * 100) / combined_results.total_trades;
                            combined_results.total_trades_lost_percentage = (combined_results.total_trades_lost * 100) / combined_results.total_trades;
                            combined_results.funds = (combined_results.funds + results_final_record_stats_current.funds);
                        }
                        */
                    }

                }

                var combined_results_true_trades = 0;
                foreach (var results_final_record_stats_current in stats)
                {

                    if (results_final_record_stats_current.total_trades > 0)
                    {
                        combined_results_true_trades++;
                        combined_results.total_trades = (combined_results.total_trades + results_final_record_stats_current.total_trades);
                        combined_results.total_trades_lost = (combined_results.total_trades_lost + results_final_record_stats_current.total_trades_lost);
                        combined_results.total_trades_won = (combined_results.total_trades_won + results_final_record_stats_current.total_trades_won);
                        combined_results.total_trades_won_percentage = (combined_results.total_trades_won * 100) / combined_results.total_trades;
                        combined_results.total_trades_lost_percentage = (combined_results.total_trades_lost * 100) / combined_results.total_trades;
                        combined_results.funds = (combined_results.funds + results_final_record_stats_current.funds);
                    }

                }

                if (combined_results_true_trades > 0)
                {
                    combined_results.funds = combined_results.funds / combined_results_true_trades;
                }

                stats = stats.OrderByDescending(x => x.total_trades_won_percentage).ToList();
                _JsonObjectResult.JsonObjectResultStatsRecords = stats;

                _JsonObjectResult.combined_results = combined_results;
            }
            catch (Exception ex)
            {
                _JsonObjectResult.result = "ERROR";
                _JsonObjectResult.description = ex.Message;
            }
            return _JsonObjectResult;
        }

    }
}
