using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ThirtyDollarWindowsGuiApp.Managers.Objects
{
    internal class ProgramSettings
    {
        [JsonInclude]
        public string AudioFileLocation = "./Samples";
        [JsonInclude]
        public string SampleBaseUrl = "https://thirtydollar.website/sounds";
        [JsonInclude]
        public string ThirtyDollarSiteUrl = "https://thirtydollar.website";
    }
}
