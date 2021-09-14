using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace SimpleAirDefense.Encyclopedia
{
    public class UnitEntry
    {
        public string Name { get; set; }
        public string Code { get; set; }
        [YamlMember(Alias = "mil_std_2525_d")]
        public string MilStd2525D { get; set; }
        public List<string> DcsCodes { get; set; }
    }
}
