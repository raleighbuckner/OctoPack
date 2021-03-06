using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace OctoPack.Tasks
{
    public class GetAssemblyVersionInfo : AbstractTask
    {

        /// <summary>
        /// Specifies the files the retrieve info from.
        /// </summary>
        [Required]
        public ITaskItem[] AssemblyFiles { get; set; }

        /// <summary>
        /// Contains the retrieved version info
        /// </summary>
        [Output]
        public ITaskItem[] AssemblyVersionInfo { get; set; }

        public override bool Execute()
        {
            if (AssemblyFiles.Length <= 0)
            {
                return false;
            }

            var infos = new List<ITaskItem>();
            foreach (var assemblyFile in AssemblyFiles)
            {
                LogMessage(String.Format("Get version info from assembly: {0}", assemblyFile), MessageImportance.Normal);

                infos.Add(CreateTaskItemFromFileVersionInfo(assemblyFile.ItemSpec));
            }
            AssemblyVersionInfo = infos.ToArray();
            return true;
        }

        public bool UseFileVersion { get; set; }
        public bool UseProductVersion { get; set; }

        private TaskItem CreateTaskItemFromFileVersionInfo(string path)
        {
            var info = FileVersionInfo.GetVersionInfo(path);

            try
            {
                return UseNuGetVersionFromGitVersionInformation(path, info);
            }
            catch (Exception ex)
            {
                LogMessage(string.Format("Could not load GitVersion information from the assembly at path {0}.", path), MessageImportance.Low);
                LogMessage(ex.ToString(), MessageImportance.Low);
            }

            return UseAssemblyVersion(info);
        }

        private TaskItem UseNuGetVersionFromGitVersionInformation(string path, FileVersionInfo info)
        {
            var nugetVersion = AssemblyExtensions.GetNuGetVersionFromGitVersionInformation(path);
            if (string.IsNullOrEmpty(nugetVersion))
            {
                throw new VersionNotFoundException(string.Format("The NuGet version obtained for GitVersion information is {0}", nugetVersion));
            }

            LogMessage(string.Format("Found GitVersion information, using version: {0}", nugetVersion),
                MessageImportance.Normal);
            // If we find a GitVersion information in the assembly, we can be pretty sure it's got the stuff we want, so let's use that.
            return new TaskItem(info.FileName, new Hashtable
                {
                    {"Version", nugetVersion},
                });
        }

        private TaskItem UseAssemblyVersion(FileVersionInfo info)
        {
            var currentAssemblyName = AssemblyName.GetAssemblyName(info.FileName);
            var assemblyVersion = currentAssemblyName.Version;
            var assemblyFileVersion = info.FileVersion;
            var assemblyVersionInfo = info.ProductVersion;

            if (UseProductVersion)
            {
                LogMessage($"Using the assembly product version because UseProductVersion is set: {assemblyVersionInfo}", MessageImportance.Normal);
                return new TaskItem(info.FileName, new Hashtable
                {
                    {"Version", assemblyVersionInfo},
                });
            }

            if (UseFileVersion || !assemblyVersionInfo.IsSemanticVersion())
            {
                if (UseFileVersion)
                {
                    LogMessage($"Using the assembly file version because UseFileVersion is set: {assemblyFileVersion}", MessageImportance.Normal);
                }
                else
                {
                    LogMessage($"Using the assembly file version because the assembly version ({assemblyVersionInfo}) is not a valid semantic version: {assemblyFileVersion}", MessageImportance.Normal);
                }
                return new TaskItem(info.FileName, new Hashtable
                {
                    {"Version", assemblyFileVersion},
                });
            }

            if (assemblyFileVersion == assemblyVersionInfo)
            {
                // Info version defaults to file version, so if they are the same, the customer probably doesn't want to use file version. Instead, use assembly version.
                return new TaskItem(info.FileName, new Hashtable
                {
                    {"Version", assemblyVersion.ToString()},
                });
            }

            // If the info version is different from file version, that must be what they want to use
            return new TaskItem(info.FileName, new Hashtable
            {
                {"Version", assemblyVersionInfo},
            });
        }
    }

    internal class VersionNotFoundException : Exception
    {
        public VersionNotFoundException(string message) : base(message)
        {
        }
    }
}
