using System.Collections.Generic;
using RurouniJones.SimpleAirDefense.Shared.Interfaces;

namespace RurouniJones.SimpleAirDefense.Shared.Models
{
    public class Unit
    {
        private IRpcClient _rpcClient;

        public Unit(IRpcClient rpcClient)
        {
            _rpcClient = rpcClient;
        }

        public uint Id { get; init; }
        public string Name { get; init; }
        public string Player { get; init; }
        public string Callsign { get; init; }
        public string GroupName { get; init; }
        public int Coalition { get; init; }
        public string Type { get; init; }
        public Position Position { get; init; }
        public double Altitude { get; init; }
        public double Heading { get; init; }
        public double Speed { get; init; }
        public bool Deleted { get; init; }
        public MilStd2525d Symbology { get; init; }

        private List<string> _attributes;
        public List<string> Attributes
        {
            get
            {
                if (_attributes != null) return _attributes;
                _attributes = _rpcClient.GetUnitDescriptorAsync(Name, Type).Result?.Attributes;
                return Attributes;
            }
            protected set => _attributes = value;
        }

        private int _alarmState;

        public int AlarmState
        {
            get => _alarmState;
            set
            {
                _rpcClient.SetAlarmStateAsync(null, GroupName, value);
                _alarmState = value;
            }
        }
    }
}
