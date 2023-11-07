using System;
using Barotrauma.MoreLevelContent.Shared.Utils;
using MoreLevelContent.Shared.Utils;
using Barotrauma;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using System.IO;
using System.Xml;

namespace MoreLevelContent.Shared.XML
{
    public class XMLManager : Singleton<XMLManager>
    {
        internal List<CustomMission> CustomMissions = new();
        public override void Setup()
        {
            List<ContentFile> otherFiles = new List<ContentFile>();
            foreach (var package in ContentPackageManager.EnabledPackages.All)
            {
                var a = package.Files.Where(f => f.GetType() == typeof(OtherFile));
                otherFiles.AddRange(a);
            }
            Log.Debug($"Collected {otherFiles.Count} other files to check");

            foreach (ContentFile file in otherFiles)
            {
                // Skip non-xml files
                if (Path.GetExtension(file.Path.RawValue) != ".xml") continue;
                XDocument doc = null;
                try
                {
                    doc = XDocument.Parse(LuaCsFile.Read(file.Path.Value));
                } catch(Exception e) 
                {
                    Log.Error($"Failed to load file at path {file.Path.Value} due to {e.Message}");
                    continue;
                }
                
                if (doc == null) { continue; }
                ContentXElement contentElement = doc.Root.FromPackage(file.ContentPackage);
                var tags = contentElement.GetAttributeStringArray("tags", Array.Empty<string>(), convertToLowerInvariant: true);
                if (!tags.Contains("MLC")) continue; // skip non-MLC elements
                foreach (string tag in tags)
                {
                    switch (tag)
                    {
                        case "mission":
                            CustomMissions.Add(new CustomMission()
                            {
                                File = new MissionsFile(file.ContentPackage, file.Path),
                                ContentXElement = contentElement
                            });
                            Log.Debug("Found custom mission");
                            break;
                        case "MLC": // Ignore MLC tag
                            break;
                        default:
                            Log.Debug($"Unknown tag: {tag}");
                            break;
                    }
                }
            }
        }
    }

    internal class CustomMission
    {
        public ContentXElement ContentXElement;
        public MissionsFile File;
    }
}
