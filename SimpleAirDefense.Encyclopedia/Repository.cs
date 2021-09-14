using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SimpleAirDefense.Encyclopedia
{
    public class Repository
    {
        private static readonly IDeserializer Deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        private static readonly HashSet<UnitEntry> Aircraft = Deserializer.Deserialize<HashSet<UnitEntry>>(
            File.ReadAllText("Data/Encyclopedia/Air.yaml"));

        private static readonly HashSet<UnitEntry> Vehicles = Deserializer.Deserialize<HashSet<UnitEntry>>(
            File.ReadAllText("Data/Encyclopedia/Land.yaml"));

        private static readonly HashSet<UnitEntry> Watercraft = Deserializer.Deserialize<HashSet<UnitEntry>>(
            File.ReadAllText("Data/Encyclopedia/Sea.yaml"));

        private static readonly HashSet<UnitEntry> UnitEntrys = BuildUnitEntryHashset();

        private static HashSet<UnitEntry> BuildUnitEntryHashset()
        {
            var set = new HashSet<UnitEntry>(Aircraft);
            set.UnionWith(Vehicles);
            set.UnionWith(Watercraft);
            return set;
        }

        public static UnitEntry GetUnitEntryByDcsCode(string code)
        {
            return UnitEntrys.FirstOrDefault(x => x.DcsCodes.Contains(code));
        }
    }
}
