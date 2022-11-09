using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;


/*
    Reverse of the strategy supertrend, when supertrend down buy and when up sell.


*/
namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None)]
    public class SuperTrendReverseStrategy : Robot
    {
        #region Inputs
        
        [Parameter("Use reverse superTrend", DefaultValue = true, Group = "Strategy settings")]
        public bool UseSuperTrend { get; set; }
        
        [Parameter("Periods", DefaultValue = 10, MinValue = 1, Step = 1, Group = "SuperTrend settings")]
        public int AtrSuperTrendPeriods { get; set; }
        
        [Parameter("Multiplier", DefaultValue = 3, MinValue = 1, Step = 1, Group = "SuperTrend settings")]
        public int AtrSuperTrendMultiplier { get; set; }
        
        [Parameter("Use EMA, Long Above, Short Under", DefaultValue = true, Group = "EMA settings")]
        public bool UseEma { get; set; }
        
        [Parameter("Source", DefaultValue = 50, MinValue = 1, MaxValue = 500, Step = 1, Group = "EMA settings")]
        public DataSeries EmaSrc { get; set; }
        
        [Parameter("Periods", DefaultValue = 200, MinValue = 1, MaxValue = 500, Step = 1, Group = "EMA settings")]
        public int EmaPeriods { get; set; }
        
        [Parameter("Lots (Forex) Shares (Stocks)", DefaultValue = 1, MinValue = 0.1, Step = 0.1, Group = "Risk settings")]
        public double Lots { get; set; }
        
        [Parameter("Stoploss (0 = Disabled)", DefaultValue = 20, MinValue = 0, Step = 1, Group = "Risk settings")]
        public double StopLoss { get; set; }
        
        [Parameter("TakeProfit (0 = Disabled)", DefaultValue = 40, MinValue = 0, Step = 1, Group = "Risk settings")]
        public double TakeProfit { get; set; }
        
        [Parameter("Move to BreakEven at 1:1 RU - (SL = 0 Then disabled)", DefaultValue = false, Group = "Risk settings")]
        public bool MoveToBreakEvenBool { get; set; }
        
        [Parameter("Use trailing stop (Uses stop loss or ATR) (if SL = 0 then ATR)", DefaultValue = false, Group = "Risk settings")]
        public bool UseTrailing { get; set; }
        
        [Parameter("ATR periods", DefaultValue = 14, MinValue = 1, Step = 1, Group = "ATR settings")]
        public int AtrPeriods { get; set; }
        
        [Parameter("Trailing X * ATR", DefaultValue = 1, MinValue = 1, Step = 1, Group = "ATR settings")]
        public double UseTrailingAtrMultiplier { get; set; }
        

        #endregion


        #region Variables

        private double superTrendUp;
        private double superTrendDown;
        private double EMA;
        private double ATR;
        private string SuperTrendLoc;
        
        private int ColorSuperTIndex;
        #endregion
        
        
        protected override void OnStart()
        {
            Positions.Closed += OnPositionClose;
        }


        protected override void OnTick()
        {
            EMA = Indicators.SimpleMovingAverage(EmaSrc, EmaPeriods).Result.LastValue;
            ATR = Indicators.AverageTrueRange(AtrPeriods, MovingAverageType.Simple).Result.LastValue;
            CheckSuperTrend();
            
            ColorCandleInTrade();
            
            MoveToBreakEven();
            Trailing();
            //ColorStopLoss(Color.Yellow);
            
        }


        protected override void OnStop()
        {
            // Handle cBot stop here
        }


        #region Strategy
        
        private void CheckSuperTrend()
        {
            superTrendUp = Indicators.Supertrend(AtrSuperTrendPeriods,AtrSuperTrendMultiplier).UpTrend.LastValue;
            superTrendDown = Indicators.Supertrend(AtrSuperTrendPeriods,AtrSuperTrendMultiplier).DownTrend.LastValue;
            if(!double.IsNaN(superTrendDown))
            {
                ClosePositions("DOWN");
                
                if(UseSuperTrend & CheckEma(true))
                    ExicuteTrade(TradeType.Buy, "DOWN");
                else if(!UseSuperTrend & CheckEma())
                    ExicuteTrade(TradeType.Sell, "DOWN");
                    
                ColorSuperTrend(Color.Red, superTrendDown);
            }
            else if(!double.IsNaN(superTrendUp))
            {   
                ClosePositions("UP");
                
                if(UseSuperTrend & CheckEma())
                    ExicuteTrade(TradeType.Sell, "UP");
                else if(!UseSuperTrend & CheckEma(true))
                    ExicuteTrade(TradeType.Buy, "UP");
                    
                ColorSuperTrend(Color.Green, superTrendUp);
            }
        }
        
        
        private bool CheckEma(bool above = false)
        {
            int index = Bars.Count - 1;
            double high = Bars.HighPrices[index];
            double low = Bars.LowPrices[index];
            if(UseEma)
            {   
                if(above)
                {
                    if(EMA < high & EMA < low)
                    {
                        return true;
                    }
                    return false;
                }
                else
                {
                    if(EMA > high & EMA > low)
                    {
                        return true;
                    }
                    return false;
                }
            }
            return true;
        
        }

        #endregion


        #region ClosePos, ConvertVolum, Exicute Trade, OnPositionClose

        private void ClosePositions(string superTrendL)
        {
            if(Positions.Count > 0 & superTrendL != SuperTrendLoc)
            {
                var pos = Positions.First();
                ClosePosition(pos);
                ColorCandleStartEnd(Color.DeepPink);
                SuperTrendLoc = "";
            }
        }
         
        
        
        private void ExicuteTrade(TradeType TrType, string superTrendL)
        {
            if(Positions.Count > 0)
                return;

            SuperTrendLoc = superTrendL;
            
            var sl = CalcStopLoss();
            var tp = CalcTakeProfit();
            var vol = DetermineTradeSize();
            
            var res = ExecuteMarketOrder(TrType, Symbol.Name, vol, "SuperTrend", sl, tp, "SuperTrend", MoveToBreakEvenBool);
            if(res.IsSuccessful)
            {
                Print("StopLoss: ", res.Position.StopLoss);
                Print("TakeProfit: ", res.Position.TakeProfit);
                ColorCandleStartEnd(Color.Orange);
            }
        }
        
        
        private void OnPositionClose(PositionClosedEventArgs args)
        {
            Print("Position Closed");
            // Stop coloring candles bc SL might have been triggered
        }

        #endregion


        #region Helpers
        
        private void ColorSuperTrend(Color col, double y)
        {
            if(ColorSuperTIndex != Bars.Count - 1)
            {
                Chart.DrawText(string.Format("{0}", RandomNum()), "•", Bars.Count - 1, y, col);
                ColorSuperTIndex = Bars.Count - 1;
            }
        }
        
        
        private void ColorCandleInTrade()
        {
            if(Positions.Count > 0)
            {
                if(Positions.First().TradeType == TradeType.Buy)
                {
                    Chart.SetBarColor(Bars.Count - 1, UseSuperTrend ? Color.Red : Color.Green);
                }
            }

            if(Positions.Count > 0)
            {
                if(Positions.First().TradeType == TradeType.Sell)
                {
                    Chart.SetBarColor(Bars.Count - 1, UseSuperTrend ? Color.Green : Color.Red);
                }
            }
        } 
        
        
        private void ColorCandleStartEnd(Color col)
        {
            Chart.SetBarColor(Bars.Count - 1, col);
        }
        
        
        private void ColorStopLoss(Color col)
        {
            if(Positions.Count > 0)
            {
                int index = Bars.Count - 1;
                double posSl = (double) Positions.First().StopLoss;
                Chart.DrawText(string.Format("{0}", RandomNum()), "•", index, posSl, col);
            }
        }
        
        
        private int RandomNum()
        {
            Random r = new Random();
            return r.Next(0, 1000000);
        }
        
        #endregion
        
        
        #region Sl TP Risk Sharesize
        private double DetermineTradeSize()
        {
            if(Symbol.TickSize == 0.01)
            {
                // Stocks so its share size
                return Lots;
            }
            else 
            {
                // Forex
                return Symbol.NormalizeVolumeInUnits(Lots * 100000);;
            }
        }
        
        
        
        private double CalcStopLoss()
        {
            if(Symbol.TickSize == 0.01)
            {
                return (StopLoss * Symbol.TickSize);
            }
            else 
            {
                // Forex
                return StopLoss;
            }
        }
        
        private double CalcTakeProfit()
        {
            if(Symbol.TickSize == 0.01)
            {
                // Stocks so its share size
                return TakeProfit * Symbol.TickSize;
            }
            else 
            {
                // Forex
                return TakeProfit;
            }
        }

        #endregion
        
        #region BreakEven, Trailing

        private void MoveToBreakEven()
        {

            if (Positions.Count == 0 | !MoveToBreakEvenBool)
            {
                return;
            }
            var position = Positions.First();
            if(position.Pips >= StopLoss)
            {
                double add = position.TradeType == TradeType.Buy ? Symbol.TickSize * 5 : -Symbol.TickSize * 5;
                ModifyPosition(position, position.EntryPrice + add, position.TakeProfit);
                Print("Position has been moved to breakeven");
            }  
        }
        
        private void Trailing()
        {

            if (Positions.Count == 0 | !UseTrailing)
            {
                return;
            }

            var position = Positions.First();
            
            
            double distance = position.TradeType == TradeType.Buy ? (Symbol.Bid - position.EntryPrice) : (position.EntryPrice - Symbol.Ask);


            double newStopLossPrice = 0;
            
            if(StopLoss > 0 & distance > 0)
            {
                if(position.TradeType == TradeType.Buy)
                {
                    newStopLossPrice = Math.Round(Symbol.Bid - StopLoss * Symbol.PipSize, Symbol.Digits);
                }
                else
                {
                    newStopLossPrice = Math.Round(Symbol.Ask + StopLoss * Symbol.PipSize, Symbol.Digits);
                } 
            }
            else if(StopLoss == 0 & distance > 0)
            {
                if(position.TradeType == TradeType.Buy)
                {
                    newStopLossPrice = Math.Round(Symbol.Bid - (ATR * UseTrailingAtrMultiplier), Symbol.Digits);
                }
                else
                {
                    newStopLossPrice = Math.Round(Symbol.Ask + (ATR * UseTrailingAtrMultiplier), Symbol.Digits);
                }
                

            }

            if(newStopLossPrice != 0)
            {
                bool ShortOrLong = position.TradeType == TradeType.Buy ? newStopLossPrice > position.StopLoss : newStopLossPrice < position.StopLoss;
                
                if(position.StopLoss == null | ShortOrLong)
                {
                    //Chart.DrawText(string.Format("{0}", RandomNum()), "-", Bars.Count - 1, newStopLossPrice, Color.Yellow);
                    ModifyPosition(position, newStopLossPrice, position.TakeProfit);
                }
            }

        }
        
        #endregion
    }
}
