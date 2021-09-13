using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RurouniJones.SimpleAirDefense.Shared.Models;
using YamlDotNet.Serialization;

namespace RurouniJones.SimpleAirDefense.Grpc.Cache
{
    public class UnitDescriptorCache
    {
        private readonly ConcurrentDictionary<string, UnitDescriptor> _descriptorCache = new();
        private const string DescriptorCacheDirectory = "Cache/Descriptors/";
       
        private readonly ILogger<UnitDescriptorCache> _logger;

        public UnitDescriptorCache(ILogger<UnitDescriptorCache> logger)
        {
            _logger = logger;
            PopulateDescriptorCache();
        }

        private void PopulateDescriptorCache()
        {
            try
            {
                Directory.CreateDirectory(DescriptorCacheDirectory);

                var deserializer = new DeserializerBuilder()
                    .Build();

                foreach (var file in Directory.EnumerateFiles(DescriptorCacheDirectory, "*.yaml"))
                {
                    _descriptorCache[Path.GetFileNameWithoutExtension(file)] =
                        deserializer.Deserialize<UnitDescriptor>(File.ReadAllText(file));
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Error building descriptor cache");
            }
        }

        public UnitDescriptor GetDescriptor(string name)
        {
            return _descriptorCache[name];
        }

        public async Task AddDescriptorToCacheAsync(string type, UnitDescriptor descriptor)
        {
            var serializer = new SerializerBuilder().Build();
            var yaml = serializer.Serialize(descriptor);

            await File.WriteAllTextAsync($"{DescriptorCacheDirectory}/{type}.yaml", yaml);

            _descriptorCache[type] = descriptor;
        }
    }
}
