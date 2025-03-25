using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zhger.Net.Ftp
{
    public class FtpFeatures
    {

        private readonly Dictionary<string, string> _features;

        public FtpFeatures()
        {
            _features = new();
        }
        public FtpFeatures(IEnumerable<string> features)
        {
            _features = features
                .Select(t =>
                {
                    int idx = t.IndexOf(" ");
                    if (idx == -1)
                    {
                        return new KeyValuePair<string, string>(t, "");
                    }
                    return new KeyValuePair<string, string>(t.Substring(0, idx), t.Substring(idx + 1).TrimStart());
                })
                .ToDictionary(t => t.Key, t => t.Value);
        }

        public bool Has(string feature)
        {
            return _features.ContainsKey(feature);
        }
        public bool TryGet(string feature, out string options)
        {
            return _features.TryGetValue(feature, out options);
        }
        public string Get(string feature, string defaultValue = null)
        {
            if(_features.TryGetValue(feature, out string options)) return options;

            return defaultValue;
        }

        public IEnumerable<string> All => _features.Keys;
    }
}
