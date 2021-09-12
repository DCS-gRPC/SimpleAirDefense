using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RurouniJones.SimpleAirDefense.Core;

namespace RurouniJones.SimpleAirDefense
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly List<Defender> _Defenders = new();

        public Worker(ILogger<Worker> logger, IOptions<Configuration> configuration, DefenderFactory DefenderFactory)
        {
            _logger = logger;

            foreach (var gameServer in configuration.Value.GameServers)
            {
                _logger.LogInformation("Instantiating {shortName} Defender", gameServer.ShortName);
                var defender = DefenderFactory.CreateDefender();
                defender.GameServer = gameServer;
                _Defenders.Add(defender);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var defenderTasks = new List<Task>();
            _Defenders.ForEach(s => defenderTasks.Add(s.ExecuteAsync(stoppingToken)));
            await Task.WhenAll(defenderTasks);
        }
    }
}
