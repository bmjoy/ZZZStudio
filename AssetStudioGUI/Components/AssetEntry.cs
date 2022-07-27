using System.Windows.Forms;
using AssetStudio;

namespace AssetStudioGUI
{
    internal class AssetEntry
    {
        public string Name { get; set; }
        public string SourcePath { get; set; }
        public long PathID { get; set; }
        public ClassIDType Type { get; set; }

        public AssetEntry(string name, string sourcePath, long pathId, ClassIDType type)
        {
            Name = name;
            SourcePath = sourcePath;
            Type = type;
            PathID = pathId;
        }
    }
}
