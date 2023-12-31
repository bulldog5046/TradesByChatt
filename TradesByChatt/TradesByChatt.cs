﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace TradesByChatt
{

	public class TradesByChatt : Strategy, ICurrentSymbol, ICurrentAccount
    {
        [InputParameter("Symbol", 10)]
        public Symbol CurrentSymbol { get; set; }

        [InputParameter("Account", 20)]
        public Account CurrentAccount { get; set; }

        [InputParameter("Video ID", 30)]
        public string VideoId;

        [InputParameter("Timer Interval (Sec)", 40)]
        public int TimerIntervalSeconds = 30;

        public override string[] MonitoringConnectionsIds => new string[] { this.CurrentSymbol?.ConnectionId };

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private YoutubeLiveVoteCollector voteCollector;

        private System.Timers.Timer countdownTimer;
        private DateTime timerStarted;

        private HistoricalData trigger;

        public TradesByChatt()
            : base()
        {
            this.Name = "TradesByChatt";
            this.Description = "YouTube Live Chat Driven Trading";
        }


        protected override void OnRun()
        {
            if (CurrentSymbol == null || CurrentAccount == null || CurrentSymbol.ConnectionId != CurrentAccount.ConnectionId)
            {
                Log("Incorrect input parameters... Symbol or Account are not specified or they have diffent connectionID.", StrategyLoggingLevel.Error);
                return;
            }

            this.CurrentSymbol = Core.GetSymbol(this.CurrentSymbol?.CreateInfo());

            if (this.CurrentSymbol != null)
            {
                InitializeTimer(TimerIntervalSeconds * 1000);
                this.countdownTimer.Elapsed += ExecuteVotes;
                this.timerStarted = DateTime.Now;
                this.countdownTimer.Start();

                this.voteCollector = new YoutubeLiveVoteCollector(this.VideoId, this);

                // Start collecting votes in a separate task
                cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => this.voteCollector.StartCollectingVotes(cancellationTokenSource.Token));

            }

        }

        protected override void OnStop()
        {
            if (this.CurrentSymbol != null)
            {
                this.countdownTimer.Elapsed -= ExecuteVotes;

                this.cancellationTokenSource.Cancel();

                countdownTimer.Stop();
            }

        }

        protected override void OnRemove()
        {
            this.CurrentSymbol = null;
            this.CurrentAccount = null;
        }

        protected override List<StrategyMetric> OnGetMetrics()
        {
            List<StrategyMetric> result = base.OnGetMetrics();

            try
            {
                var votes = voteCollector.GetVoteResults();


                result.Add("Countdown", GetRemainingTime());
                result.Add("Buy votes", votes["1"]);
                result.Add("Sell votes", votes["2"]);
                result.Add("Flatten votes", votes["3"]);
                result.Add("Balance", CurrentAccount.Balance);
            }
            catch (Exception ex)
            {
                // do nothing
            }

            return result;
        }

        private void ExecuteVotes(object sender, EventArgs e)
        {
            // reset timer tracking
            this.timerStarted = DateTime.Now;

            var votes = voteCollector.GetVoteResults();
            var highestVoteKey = votes.OrderByDescending(v => v.Value).FirstOrDefault().Key;
            var topTwoVotes = votes.OrderByDescending(v => v.Value).Take(2).ToList();
            bool isTie = topTwoVotes[0].Value == topTwoVotes[1].Value;


            if (highestVoteKey == "0" || isTie)
            {
                Log("Vote Tie. Doing nothing.", StrategyLoggingLevel.Error);
                return;
            }

            switch(highestVoteKey)
            {
                case "1":
                    Log("Buy Wins!", StrategyLoggingLevel.Trading);
                    ExecuteOrder(Side.Buy);
                    break;

                case "2":
                    Log("Sell Wins!", StrategyLoggingLevel.Trading);
                    ExecuteOrder(Side.Sell);
                    break;
                    
                case "3":
                    Log("Flat Wins!", StrategyLoggingLevel.Trading);
                    FlattenPosition();
                    break;

                default:
                    Log("No suitable Vote", StrategyLoggingLevel.Error);
                    break;
            }

            voteCollector.ResetCounters();
        }

        private void ExecuteOrder(Side side)
        {
            Position position = Core.Instance.Positions.Where(x => x.Symbol == CurrentSymbol && x.Account == CurrentAccount).FirstOrDefault();

            if (position == null)
            {
                var result = Core.Instance.PlaceOrder(CurrentSymbol, CurrentAccount, side: side, quantity: 1);
                Log($"Order: {result.OrderId}", StrategyLoggingLevel.Trading);
            } else if (position.Side == side)
            {
                Log("Already in a position, skipping");
            }
            
        }

        private void FlattenPosition()
        {
            Position position = Core.Instance.Positions.Where(x => x.Symbol == CurrentSymbol && x.Account == CurrentAccount).FirstOrDefault();

            if (position != null)
            {
                position.Close();
                Log($"Position Closed", StrategyLoggingLevel.Trading);
            }
        }

        private void InitializeTimer(double intervalInMilliseconds)
        {
            this.countdownTimer = new System.Timers.Timer(intervalInMilliseconds);
            this.countdownTimer.AutoReset = true;
        }

        public string GetRemainingTime()
        {
            double elapsedMilliseconds = (DateTime.UtcNow - timerStarted).TotalMilliseconds;
            double remainingMilliseconds = TimerIntervalSeconds * 1000 - elapsedMilliseconds;
            if (remainingMilliseconds < 0) remainingMilliseconds = 0;

            TimeSpan remainingTimeSpan = TimeSpan.FromMilliseconds(remainingMilliseconds);
            return $"{remainingTimeSpan.Minutes:D2}:{remainingTimeSpan.Seconds:D2}"; 
        }


    }
}
