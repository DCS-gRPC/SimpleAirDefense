using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Geo.Geodesy;
using Microsoft.Extensions.Logging;
using RurouniJones.SimpleAirDefense.Shared.Interfaces;
using RurouniJones.SimpleAirDefense.Shared.Models;

namespace RurouniJones.SimpleAirDefense.Core
{
    public class Defender
    {
        // These values are taken from Wheelyjoes IADS script.
        // https://github.com/wheelyjoe/DCS-Scripts/blob/master/IADS.lua
        private readonly Dictionary<string, int> _samRanges = new()
        {
            { "Kub 1S91 str", 52000 },
            { "S-300PS 40B6M tr", 100000 },
            { "Osa 9A33 ln", 25000 },
            { "snr s-125 tr", 60000 },
            { "SNR_75V", 65000 },
            { "Dog Ear radar", 26000 },
            { "SA-11 Buk LN 9A310M1", 43000 },
            { "Hawk tr", 60000 },
            { "Tor 9A331", 50000 },
            { "rapier_fsa_blindfire_radar", 6000 },
            { "Patriot STR", 100000 },
            { "Roland ADS", 10000 },
            { "HQ-7_STR_SP", 12500 },
            { "ZSU-23-4 Shilka", 1000 }
        };

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
                var defenderTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var defenderToken = defenderTokenSource.Token;

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
                    _rpcClient.ExecuteAsync(defenderToken), // Get the events and put them into the queue
                    ProcessQueue(queue, defenderToken), // Process the queue events into the units dictionary
                    MonitorAirspace(defenderToken) // Main processing
                };
                await Task.WhenAny(tasks); // If one task finishes (usually when the RPC client gets
                                           // disconnected on mission restart
                _logger.LogInformation("{server} Defender Processing stopping", GameServer.ShortName);
                defenderTokenSource.Cancel(); // Then cancel all of the other tasks
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

        private async Task MonitorAirspace(CancellationToken defenderToken)
        {
            _logger.LogInformation("Airspace monitoring started");
            while (!defenderToken.IsCancellationRequested)
            {
                await Task.Delay(10000, defenderToken);

                try
                {
                    _logger.LogInformation("Entering Monitoring Loop");

                    // Skip if there are no units
                    if (_units.Values.Count == 0)
                    {
                        _logger.LogInformation("No Units found. Skipping");
                        continue;
                    }

                    var alarmStates = new Dictionary<string, int>();
                    // Check to see if there are any active EWRs
                    var ewrsPresent = _units.Values.ToList().Any(u => u.Attributes.Contains("EWR"));

                    if (!ewrsPresent)
                    {
                        _logger.LogInformation("No EWRs found. Turning on all SAM sites");
                        foreach (var samSite in _units.Values.Where(u => u.Attributes.Contains("SAM TR")))
                        {
                            _logger.LogInformation("{unitName}, {groupName}: Turning on radar", samSite.Name, samSite.GroupName);
                            samSite.AlarmState = 2;
                        }
                    }
                    else
                    {
                        _logger.LogInformation("EWR sites found");
                        foreach (var samSite in _units.Values.Where(u => u.Attributes.Contains("SAM TR")))
                        {
                            _logger.LogInformation("{unitName}, {groupName}: Checking if targets in activation range", samSite.Name, samSite.GroupName);

                            var samSitePosition =
                                new Geo.Coordinate(samSite.Position.Latitude, samSite.Position.Longitude);
                            var targetsInRange = _units.Values.Any(u =>
                            {
                                var unitPosition = new Geo.Coordinate(u.Position.Latitude, u.Position.Longitude);
                                return u.Coalition != samSite.Coalition &&
                                       samSitePosition.CalculateGreatCircleLine(unitPosition).Distance.SiValue <
                                       _samRanges[samSite.Type];
                            });

                            /*
                             * We should always be able to enable a SamSite because there might be a longer ranged radar in it. But we shouldn't shut down a sam site
                             * because one of the Radars is shorter range (i.e. shut down an SA-6 site because it has a Short Range Shilka in it)
                             */
                            if (targetsInRange)
                            {
                                _logger.LogInformation("{unitName}, {groupName}: Targets in activation range", samSite.Name, samSite.GroupName);
                                _logger.LogInformation("{unitName}, {groupName}: Setting Alarm State to {alarmState}", samSite.Name, samSite.GroupName, 2);
                                alarmStates[samSite.GroupName] = 2;

                            }
                            else
                            {
                                _logger.LogInformation("{unitName}, {groupName}: No targets in activation range", samSite.Name, samSite.GroupName);
                                if (alarmStates.ContainsKey(samSite.GroupName))
                                {
                                    _logger.LogInformation("{unitName}, {groupName}: Existing Alarm state set, skipping", samSite.Name, samSite.GroupName);
                                }
                                else
                                {
                                    _logger.LogInformation("{unitName}, {groupName}: Turning alarm state to {alarmState}", samSite.Name, samSite.GroupName, 1);
                                    alarmStates[samSite.GroupName] = 1;
                                }
                            }
                        }

                        foreach (var (groupName, alarmState) in alarmStates)
                        {
                            var unit = _units.Values.First(u => u.GroupName == groupName);
                            _logger.LogInformation("{groupName}: Setting entire site alarm State to {alarmState}", unit.GroupName, alarmState);
                            unit.AlarmState = alarmState;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Airspace monitoring failure");
                }
            }
        }
    }
}
