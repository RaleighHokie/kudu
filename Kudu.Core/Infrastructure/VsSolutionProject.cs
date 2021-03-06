﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Kudu.Core.Infrastructure {
    [DebuggerDisplay("{ProjectName}")]
    public class VsSolutionProject {
        private const string ProjectInSolutionTypeName = "Microsoft.Build.Construction.ProjectInSolution, Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";

        private static readonly Guid _wapGuid = new Guid("349c5851-65df-11da-9384-00065b846f21");

        private static readonly Type _projectInSolutionType;
        private static readonly PropertyInfo _projectNameProperty;
        private static readonly PropertyInfo _relativePathProperty;
        private static readonly PropertyInfo _projectTypeProperty;

        static VsSolutionProject() {
            _projectInSolutionType = Type.GetType(ProjectInSolutionTypeName, throwOnError: false, ignoreCase: false);

            if (_projectInSolutionType != null) {
                _projectNameProperty = ReflectionUtility.GetInternalProperty(_projectInSolutionType, "ProjectName");
                _relativePathProperty = ReflectionUtility.GetInternalProperty(_projectInSolutionType, "RelativePath");
                _projectTypeProperty = ReflectionUtility.GetInternalProperty(_projectInSolutionType, "ProjectType");
            }
        }

        public IEnumerable<Guid> ProjectTypeGuids { get; set; }
        public string ProjectName { get; private set; }
        public string AbsolutePath { get; private set; }
        public bool IsWebSite { get; private set; }
        public bool IsWap { get; set; }

        public VsSolutionProject(string solutionPath, object project) {
            ProjectName = _projectNameProperty.GetValue<string>(project);
            var relativePath = _relativePathProperty.GetValue<string>(project);
            var projectType = _projectTypeProperty.GetValue<SolutionProjectType>(project);

            AbsolutePath = Path.Combine(Path.GetDirectoryName(solutionPath), relativePath);
            IsWebSite = projectType == SolutionProjectType.WebProject;

            if (projectType == SolutionProjectType.KnownToBeMSBuildFormat) {
                // If the project is an msbuild project then extra the project type guids
                ProjectTypeGuids = GetProjectTypeGuids(AbsolutePath);

                // Check if it's a wap
                IsWap = ProjectTypeGuids.Contains(_wapGuid);
            }
            else {
                ProjectTypeGuids = Enumerable.Empty<Guid>();
            }
        }

        private static IEnumerable<Guid> GetProjectTypeGuids(string path) {
            var document = XDocument.Parse(File.ReadAllText(path));

            var guids = from propertyGroup in document.Root.Elements(GetName("PropertyGroup"))
                        let projectTypeGuids = propertyGroup.Element(GetName("ProjectTypeGuids"))
                        where projectTypeGuids != null
                        from guid in projectTypeGuids.Value.Split(';')
                        select new Guid(guid.Trim('{', '}'));
            return guids;
        }

        private static XName GetName(string name) {
            return XName.Get(name, "http://schemas.microsoft.com/developer/msbuild/2003");
        }

        // Microsoft.Build.Construction.SolutionProjectType
        private enum SolutionProjectType {
            Unknown,
            KnownToBeMSBuildFormat,
            SolutionFolder,
            WebProject,
            WebDeploymentProject,
            EtpSubProject,
        }
    }
}
