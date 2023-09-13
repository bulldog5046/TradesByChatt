using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YoutubeLiveChatSharp;
using System.Linq;
using System.Threading;
using TradingPlatform.BusinessLayer;

public class YoutubeLiveVoteCollector
{
    private readonly TradesByChatt.TradesByChatt _instance;
    private readonly ChatFetch _fetch;
    private readonly HashSet<string> _votedUserIds = new HashSet<string>();
    private readonly Dictionary<string, int> _digitCounters = new Dictionary<string, int>
    {
        {"0", 0},
        {"1", 0},
        {"2", 0},
        {"3", 0},
        {"4", 0},
        {"5", 0},
        {"6", 0},
        {"7", 0},
        {"8", 0},
        {"9", 0},
    };

    public YoutubeLiveVoteCollector(string videoId, TradesByChatt.TradesByChatt instance)
    {
        _fetch = new ChatFetch(videoId);
        _instance = instance;
        _instance.LogTrading("Starting vote collection...");
    }

    public async Task StartCollectingVotes(CancellationToken token)
    {
        while (true)
        {
            if (token.IsCancellationRequested)
            {

                _instance.LogError("Stopping vote collection...");
                break;
            }

            _fetch.Fetch().ToList().ForEach(c => ProcessComment(c));

            // Your existing code...

            await Task.Delay(1000);
        }
    }


    private void ProcessComment(Comment c)
    {
        if (_votedUserIds.Contains(c.userId))
            return;

        Match match = Regex.Match(c.text, @"^\d$");
        if (match.Success)
        {
            _votedUserIds.Add(c.userId);
            _digitCounters[match.Value]++;
            _instance.LogTrading($"Vote received from user {c.userName} : {match.Value}");
        }
    }

    public void ResetCounters()
    {
        _votedUserIds.Clear();
        foreach (var key in _digitCounters.Keys.ToList())
        {
            _digitCounters[key] = 0;
        }
        _instance.LogInfo("Counters have been reset!");
    }

    public Dictionary<string, int> GetVoteResults()
    {
        return new Dictionary<string, int>(_digitCounters);
    }

    



}
