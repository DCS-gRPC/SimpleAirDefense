using System.Collections.Generic;

namespace RurouniJones.SimpleAirDefense.Core
{
    public sealed class Configuration
    {
        public List<GameServer> GameServers { get; init; }
    }

    public sealed class GameServer
    {
        public string Name { get; set; }
        public string ShortName { get; set; }
        public Rpc Rpc { get; set; }
    }

    public sealed class Rpc
    {
        public string Host { get; set; }
        public int Port { get; set; }
    }
}
