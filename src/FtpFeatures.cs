using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zhger.Net.Ftp
{
    public class FtpFeatures
    {

        private readonly NameValueCollection _features = [];
        private readonly HashSet<string> _keys;

        public FtpFeatures()
        {
            _keys = [];
        }
        public FtpFeatures(IEnumerable<string> features)
        {
            foreach (string feature in features) {
                if(string.IsNullOrEmpty(feature)) continue;
                var values = feature.Split([' '], 2);

                if (values.Length == 1)
                {
                    _features.Add(values[0], "");
                    continue;
                }
                _features.Add(values[0], values[1]);
            }
            _keys = [.. _features.Keys.Cast<string>()];
        }

        public bool Has(string feature)
        {
            return _keys.Contains(feature);
        }
        public bool TryGet(string feature, out string options)
        {
            options = null;
            if (!_keys.Contains(feature)) return false;
            options = string.Join(", ", _features.GetValues(feature));
            return true;
        }
        public string Get(string feature, string defaultValue = null)
        {
            if(_keys.Contains(feature)) return  string.Join(", ", _features.GetValues(feature));

            return defaultValue;
        }

        public IEnumerable<string> All => _keys;
    }
}
