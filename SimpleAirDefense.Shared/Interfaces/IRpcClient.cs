﻿using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using RurouniJones.SimpleAirDefense.Shared.Models;

namespace RurouniJones.SimpleAirDefense.Shared.Interfaces
{
    public interface IRpcClient
    {
        /*
         * A queue containing all the unit updates to be processed. We populate
         * this queue in a separate thread to make sure that slowdowns in unit
         * processing do not impact the rate at which we can receive unit updates
        */
        public ConcurrentQueue<Unit> UpdateQueue { get; set; }

        /*
         * Hostname of the RPC server we are connecting to
         */
        public string HostName { get; set; }

        /*
         * Port number of the RPC server we are connecting to
         */
        public int Port { get; set; }

        Task ExecuteAsync(CancellationToken stoppingToken);

        Task<UnitDescriptor> GetUnitDescriptorAsync(string name, string type);

        Task SetAlarmStateAsync(string unitName, string groupName, int alarmState);
    }
}