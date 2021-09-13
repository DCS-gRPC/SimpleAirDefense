using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using RurouniJones.SimpleAirDefense.Shared.Interfaces;
using RurouniJones.SimpleAirDefense.Shared.Models;

namespace RurouniJones.SimpleAirDefense.Grpc
{
    public class RpcClient : IRpcClient
    {
        public ConcurrentQueue<Shared.Models.Unit> UpdateQueue { get; set; }

        public string HostName { get; set; }
        public int Port { get; set; }

        private readonly ILogger<RpcClient> _logger;

        public RpcClient(ILogger<RpcClient> logger)
        {
            _logger = logger;
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
                                Symbology = null
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
            // TODO: Cache Descriptors
            // Descriptors do not change. Therefore we should cache the results of this call
            // on the typeName provided in the descriptor. If one exists then we use that. If
            // one doesn't then we get the descriptor for this named unit and then cache the
            // response based on the TypeName
            _logger.LogInformation("{name} ({type}) Retrieving Descriptor", name, type);
            using var channel = GrpcChannel.ForAddress($"http://{HostName}:{Port}");
            var client = new Units.UnitsClient(channel);

            try
            {
                var descriptor = await client.GetUnitDescriptorAsync(new GetUnitDescriptorRequest()
                {
                    Name = name
                }).ResponseAsync;

                _logger.LogInformation("{name} ({type}) Retrieved Descriptor", name, type);

                return new UnitDescriptor
                {
                    Attributes = descriptor.Attributes.ToList()
                };
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
