using System;
using System.Collections.Generic;
using System.Linq;

namespace StorjVirtualDisk
{
    public class FileReferences
    {
        public const string UNKNOWN_FILE_HASH = "UNKNOWN";

        public string Name { get; set; }

        public string Hash { get; set; }

        public string Key { get; set; }

        public long Size { get; set; }

        public DateTime? Date { get; set; }

        private IList<FileReferences> children;
        public IList<FileReferences> Children 
        {
            get { return children ?? (children = new List<FileReferences>()); }
            set { children = value; }
        }

        public bool IsFolder()
        {
            return string.IsNullOrEmpty(Hash) && string.IsNullOrEmpty(Key);
        }

        public FileReferences GetFolderReference(string path)
        {
            if (path == Name)
            {
                return this;
            }

            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            IList<string> pathParts = path.Split('\\');

            if (pathParts.Any() && (pathParts.First() == Name || (string.IsNullOrEmpty(pathParts.First()) && string.IsNullOrEmpty(Name))))
            {
                return Children.Select(child => child.GetFolderReference(string.Join(@"\", pathParts.Skip(1).ToArray()))).FirstOrDefault(child => child != null) ?? (IsFolder() ? this : null);
            }

            //return IsFolder() ? this : null;
            return string.IsNullOrEmpty(Name) ? this : null;
        }
    }
}
