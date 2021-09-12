using System;

namespace RurouniJones.SimpleAirDefense.Core
{
    public class DefenderFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefenderFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Defender CreateDefender()
        {
            return (Defender) _serviceProvider.GetService(typeof(Defender));
        }
    }
}