using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RurouniJones.SimpleAirDefense.Shared.Interfaces;
using RurouniJones.SimpleAirDefense.Shared.Models;

namespace RurouniJones.SimpleAirDefense.Core
{
    public class Defender
    {
        /*
         * Configuration for the GameServer including DB and RPC information
         */
        public GameServer GameServer { get; set; }

        /*
         * The RPC client that connects to the server and receives the unit updates
         * to put into the update queue
         */
        private readonly IRpcClient _rpcClient;

        private readonly ILogger<Defender> _logger;

        private readonly Dictionary<uint, Unit> _units = new();

        public Defender(ILogger<Defender> logger, IRpcClient rpcClient)
        {
            _logger = logger;
            _rpcClient = rpcClient;
        }

        public async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _rpcClient.HostName = GameServer.Rpc.Host;
            _rpcClient.Port = GameServer.Rpc.Port;

            _logger.LogInformation("{server} Defender Processing starting", GameServer.ShortName);
            while (!stoppingToken.IsCancellationRequested)
            {
                var scribeTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var scribeToken = scribeTokenSource.Token;

                /*
                 * A queue containing all the unit updates to be processed. We populate
                 * this queue in a separate thread to make sure that slowdowns in unit
                 * processing do not impact the rate at which we can receive unit updates
                 *
                 * We clear the queue each time we connect
                 */
                var queue = new ConcurrentQueue<Unit>();
                _rpcClient.UpdateQueue = queue;

                var tasks = new[]
                {
                    _rpcClient.ExecuteAsync(scribeToken), // Get the events and put them into the queue
                    ProcessQueue(queue, scribeToken), // Process the queue events into the units dictionary
                };
                await Task.WhenAny(tasks); // If one task finishes (usually when the RPC client gets
                                           // disconnected on mission restart
                _logger.LogInformation("{server} Defender Processing stopping", GameServer.ShortName);
                scribeTokenSource.Cancel(); // Then cancel all of the other tasks
                // Then we wait for all of them to finish before starting the loop again.
                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (Exception)
                {
                    // No-op. Exceptions have already been logged in the task
                }

                _logger.LogInformation("{server} Defender Processing stopped", GameServer.ShortName);

                // Wait before trying again unless the entire service is shutting down.
                await Task.Delay((int)TimeSpan.FromSeconds(10).TotalMilliseconds, stoppingToken);
                _logger.LogInformation("{server} Defender Processing restarting", GameServer.ShortName);
            }
        }

        private async Task ProcessQueue(ConcurrentQueue<Unit> queue, CancellationToken scribeToken)
        {

            while (!scribeToken.IsCancellationRequested)
            {
                queue.TryDequeue(out var unit);
                if (unit == null)
                {
                    await Task.Delay(5, scribeToken);
                    continue;
                }

                if (unit.Deleted)
                {
                    _units.Remove(unit.Id, out _);
                }
                else
                {
                    _units[unit.Id] = unit;
                }
            }
        }
    }
}
