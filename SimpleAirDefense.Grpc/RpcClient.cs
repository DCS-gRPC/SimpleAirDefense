using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using RurouniJones.SimpleAirDefense.Grpc.Cache;
using RurouniJones.SimpleAirDefense.Shared.Interfaces;
using RurouniJones.SimpleAirDefense.Shared.Models;
using SimpleAirDefense.Encyclopedia;

namespace RurouniJones.SimpleAirDefense.Grpc
{
    public class RpcClient : IRpcClient
    {
        public ConcurrentQueue<Shared.Models.Unit> UpdateQueue { get; set; }

        public string HostName { get; set; }
        public int Port { get; set; }

        private readonly ILogger<RpcClient> _logger;
        private readonly UnitDescriptorCache _descriptorCache;

        public RpcClient(ILogger<RpcClient> logger, UnitDescriptorCache descriptorCache)
        {
            _logger = logger;
            _descriptorCache = descriptorCache;
        }

        public async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var channel = GrpcChannel.ForAddress($"http://{HostName}:{Port}");
            var client = new Mission.MissionClient(channel);
            try
            {
                var units = client.StreamUnits(new StreamUnitsRequest
                {
                    PollRate = 1,
                    MaxBackoff = 30
                }, null, null, stoppingToken);
                await foreach (var update in units.ResponseStream.ReadAllAsync(stoppingToken))
                {
                    switch (update.UpdateCase)
                    {
                        case UnitUpdate.UpdateOneofCase.None:
                            //No-op
                            break;
                        case UnitUpdate.UpdateOneofCase.Unit:
                            var sourceUnit = update.Unit;
                            UpdateQueue.Enqueue(new Shared.Models.Unit(this)
                            {
                                Coalition = (int)sourceUnit.Coalition,
                                Id = sourceUnit.Id,
                                Name = sourceUnit.Name,
                                Position = new Shared.Models.Position(sourceUnit.Position.Lat, sourceUnit.Position.Lon),
                                Altitude = sourceUnit.Position.Alt,
                                Callsign = sourceUnit.Callsign,
                                Type = sourceUnit.Type,
                                Player = sourceUnit.PlayerName,
                                GroupName = sourceUnit.GroupName,
                                Speed = sourceUnit.Speed,
                                Heading = sourceUnit.Heading,
                                Symbology = new MilStd2525d((int) sourceUnit.Coalition, Repository.GetUnitEntryByDcsCode(sourceUnit.Type)?.MilStd2525D)
                            });
                            _logger.LogDebug("Enqueue unit update {unit}", sourceUnit);
                            break;
                        case UnitUpdate.UpdateOneofCase.Gone:
                            var deletedUnit = update.Gone;
                            UpdateQueue.Enqueue(new Shared.Models.Unit(null)
                            {
                                Id = deletedUnit.Id,
                                Name = deletedUnit.Name,
                                Deleted = true
                            });
                            _logger.LogDebug("Enqueue unit deletion {unit}", deletedUnit);
                            break;
                        default:
                            _logger.LogWarning("Unexpected UnitUpdate case of {case}", update.UpdateCase);
                            break;
                    }
                }
            }
            catch (RpcException ex)
            {
                if (ex.Status.StatusCode == StatusCode.Cancelled)
                {
                    _logger.LogInformation("Shutting down gRPC connection due to {reason}", ex.Status.Detail);
                }
                else
                {
                    _logger.LogWarning(ex, "gRPC Exception");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "gRPC Exception");
            }
        }

        public async Task<UnitDescriptor> GetUnitDescriptorAsync(string name, string type)
        {
            _logger.LogDebug("{name} ({type}) Retrieving Descriptor", name, type);

            var cachedDescriptor = _descriptorCache.GetDescriptor(type);
            if (cachedDescriptor != null)
            {
                _logger.LogDebug("{name} ({type}) Descriptor Cache hit", name, type);
                return cachedDescriptor;
            }

            _logger.LogDebug("{name} ({type}) Descriptor Cache miss", name, type);

            using var channel = GrpcChannel.ForAddress($"http://{HostName}:{Port}");
            var client = new Units.UnitsClient(channel);

            try
            {
                var descriptor = await client.GetUnitDescriptorAsync(new GetUnitDescriptorRequest()
                {
                    Name = name
                }).ResponseAsync;

                var unitDescriptor = new UnitDescriptor
                {
                    Attributes = descriptor.Attributes.ToList()
                };

                await _descriptorCache.AddDescriptorToCacheAsync(type, unitDescriptor);
                return unitDescriptor;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "gRPC Exception");
                return null;
            }
        }

        public async Task SetAlarmStateAsync(string unitName, string groupName, int alarmState)
        {
            using var channel = GrpcChannel.ForAddress($"http://{HostName}:{Port}");
            var client = new Controllers.ControllersClient(channel);

            try
            {
                var request = new SetAlarmStateRequest
                {
                    AlarmState = (SetAlarmStateRequest.Types.AlarmState)alarmState
                };
                if (unitName != null)
                {
                    request.UnitName = unitName;
                }
                else
                {
                    request.GroupName = groupName;
                }

                await client.SetAlarmStateAsync(request).ResponseAsync;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "gRPC Exception");
            }
        }
    }
}
