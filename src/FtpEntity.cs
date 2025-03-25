using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Zhger.Net.Ftp.Vendor;

namespace Zhger.Net.Ftp
{
    public enum FtpEntityType
    {
        Directory,
        File,
    }
    public class FtpEntity
    {
        public FtpEntity() { }
        public string Name { get; set; }
        public FtpEntityType Type { get; set; }

        public long Size { get; set; }

        public DateTime ModifyAt { get; set; }
    }
    internal class FtpEntityWithList: FtpEntity
    {
        internal FtpEntityWithList() { }

        public static FtpEntityWithList Parse(string item)
        {

            if (string.IsNullOrEmpty(item)) return null;

            Match match = Regex.Match(item, @"^(([0-9]{2})-([0-9]{2})-([0-9]{2})(?:\s+)((?:[0-9]{2}):(?:[0-9]{2})(?:PM|AM)))(?:\s+)(<DIR>|(?:[0-9]+))(?:\s+)(.+)$");
            if (match.Success)
            {
                FtpEntityWithList ftpEntity = new();
                ftpEntity.Type = match.Groups[6].Value == "<DIR>" ? FtpEntityType.Directory : FtpEntityType.File;
                ftpEntity.Size = match.Groups[6].Value == "<DIR>" ? 0 : long.Parse(match.Groups[6].Value);
                ftpEntity.Name = match.Groups[7].Value;
                if (DateTime.TryParse($"{match.Groups[4].Value}-{match.Groups[2].Value}-{match.Groups[3].Value} {match.Groups[5].Value}", out DateTime dateTime)) ftpEntity.ModifyAt = dateTime;

                if (ftpEntity.Name == "." || ftpEntity.Name == "..") return null;
                return ftpEntity;
            }
            return null;
        }
    }
    internal class FtpEntityWithMlsd : FtpEntity
    {
        internal FtpEntityWithMlsd() { }
        public static FtpEntityWithMlsd Parse(string item)
        {
            if (string.IsNullOrEmpty(item)) return null;

            FtpEntityWithMlsd ftpEntity = new();
            string[] items = item.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            ftpEntity.Name = items[items.Length -1].Trim();

            if (ftpEntity.Name == "." || ftpEntity.Name == "..") return null;


            var collection = items.Select(Utility.GetKeyValue);

            foreach(KeyValuePair<string,string> kv in collection)
            {
                string key_ = kv.Key.ToLower();
                string value = kv.Value;
                if (key_ == "size")
                {
                    ftpEntity.Size = long.Parse(value);
                    continue;
                }
                if (key_ == "type")
                {
                    ftpEntity.Type = value == "file" ? FtpEntityType.File : FtpEntityType.Directory;
                    continue;
                }
                if (key_ == "modify")
                {
                    string datetime = Regex.Replace(value, @"^([0-9]{4})([0-9]{2})([0-9]{2})([0-9]{2})([0-9]{2})([0-9]{2})$", "$1-$2-$3 $4:$5:$6");

                    if (DateTime.TryParse(datetime, out DateTime v)) ftpEntity.ModifyAt = v;
                    
                    continue;
                }
            }
            return ftpEntity;

        }
    }
}
