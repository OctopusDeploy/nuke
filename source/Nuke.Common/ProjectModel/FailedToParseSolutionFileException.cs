using System;
using System.IO;

namespace Nuke.Common.ProjectModel
{
    internal class FailedToParseSolutionFileException : Exception
    {
        public string SolutionFile { get; }
        public string Key { get; }

        public FailedToParseSolutionFileException(string solutionFile, string key)
            : base($"Failed to parse solution file '{Path.GetFileName(solutionFile)}' - found duplicate entry for '{key}'")
        {
            SolutionFile = solutionFile;
            Key = key;
        }
    }
}
