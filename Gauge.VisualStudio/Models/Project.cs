﻿// Copyright [2014, 2015] [ThoughtWorks Inc.](www.thoughtworks.com)
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Text;
using CodeAttributeArgument = EnvDTE80.CodeAttributeArgument;
using CodeNamespace = EnvDTE.CodeNamespace;

namespace Gauge.VisualStudio.Models
{
    public class Project
    {
        private static Events2 _events2;
        private static CodeModelEvents _codeModelEvents;
        private static List<Implementation> _implementations;
        private static DocumentEvents _documentEvents;
        private ProjectItemsEvents _projectItemsEvents;

        public Project()
        {
            _implementations = GetGaugeImplementations();
            if (_events2 != null) return;

            _events2 = GaugePackage.DTE.Events as Events2;
            _codeModelEvents = _events2.CodeModelEvents;

            _projectItemsEvents = _events2.ProjectItemsEvents;
            _documentEvents = _events2.DocumentEvents;
            _documentEvents.DocumentSaved += document => RefreshImplementations(document.ProjectItem);
            _projectItemsEvents.ItemAdded += RefreshImplementations;
            _projectItemsEvents.ItemRemoved += RefreshImplementations;
            _projectItemsEvents.ItemRenamed += (item, name) => RefreshImplementations(item);
            _codeModelEvents.ElementAdded += element => RefreshImplementations(element.ProjectItem);
            _codeModelEvents.ElementChanged += (element, change) => RefreshImplementations(element.ProjectItem);
            _codeModelEvents.ElementDeleted += (parent, element) => RefreshImplementations(element.ProjectItem);
        }

        internal static void RefreshImplementations(ProjectItem projectItem)
        {
            _implementations = GetGaugeImplementations(projectItem.ContainingProject);
        }

        internal IEnumerable<Implementation> Implementations
        {
            get { return _implementations; }
        }

        internal static void RefreshImplementationsForActiveProject()
        {
            var activeDocument = GaugePackage.DTE.ActiveDocument;
            if (activeDocument!=null)
            {
                _implementations = GetGaugeImplementations(activeDocument.ProjectItem.ContainingProject);
            }
        }

        private static List<Implementation> GetGaugeImplementations(EnvDTE.Project containingProject = null)
        {
            containingProject = containingProject ?? GaugePackage.DTE.ActiveDocument.ProjectItem.ContainingProject;
            var allClasses = GetAllClasses(containingProject);

            var gaugeImplementations = new List<Implementation>();
            gaugeImplementations.AddRange(GetStepImplementations(allClasses));

            gaugeImplementations.AddRange(new Concept(containingProject).GetAllConcepts().Select(concept => new ConceptImplementation(concept)));

            return gaugeImplementations;
        }

        private static IEnumerable<StepImplementation> GetStepImplementations(IEnumerable<CodeElement> allClasses)
        {
            var gaugeImplementations = new List<StepImplementation>();
            foreach (var codeElement in allClasses)
            {
                if (!(codeElement is CodeClass)) continue;
                var codeClass = (CodeClass) codeElement;
                var allFunctions = GetFunctionsForClass(codeClass);
                foreach (var codeFunction in allFunctions)
                {
                    var function = codeFunction as CodeFunction;
                    if (function == null) continue;
                    var allAttributes = GetCodeElementsFor(function.Attributes, vsCMElement.vsCMElementAttribute);

                    var attribute =
                        allAttributes.FirstOrDefault(a => a.Name == "Step") as
                            CodeAttribute;

                    if (attribute == null) continue;

                    var codeAttributeArguments = attribute.Children.DynamicSelect<CodeAttributeArgument>();
                    gaugeImplementations.AddRange(codeAttributeArguments.Select(argument => new StepImplementation(function, argument.Value.Trim('"'))));
                }
            }
            return gaugeImplementations;
        }

        internal Implementation GetStepImplementation(ITextSnapshotLine line)
        {
            var lineText = Step.GetStepText(line);

            return Implementations.FirstOrDefault(implementation => implementation.ContainsImplememntationFor(lineText));
        }


        internal static IEnumerable<CodeElement> GetFunctionsForClass(CodeClass codeClass)
        {
            return GetCodeElementsFor(codeClass.Members, vsCMElement.vsCMElementFunction);
        }

        internal static IEnumerable<CodeElement> GetAllClasses(EnvDTE.Project containingProject = null)
        {
            containingProject = containingProject ?? GaugePackage.DTE.ActiveDocument.ProjectItem.ContainingProject;

            return containingProject.CodeModel == null
                ? Enumerable.Empty<CodeElement>()
                : GetCodeElementsFor(containingProject.CodeModel.CodeElements, vsCMElement.vsCMElementClass);
        }

        internal static CodeClass FindOrCreateClass(string className, EnvDTE.Project project = null)
        {
            return GetAllClasses().FirstOrDefault(element => element.Name == className) as CodeClass ??
                   AddClass(className, project);
        }
        private static CodeClass AddClass(string className, EnvDTE.Project project = null)
        {
            project = project ?? GaugePackage.DTE.ActiveDocument.ProjectItem.ContainingProject;

            var codeDomProvider = CodeDomProvider.CreateProvider("CSharp");

            if (!codeDomProvider.IsValidIdentifier(className))
            {
                throw new ArgumentException(string.Format("Invalid Class Name: {0}", className));
            }
            
            var targetClass = codeDomProvider.CreateValidIdentifier(className);


            string targetNamespace;
            try
            {
                targetNamespace = project.Properties.Item("DefaultNamespace").Value.ToString();
            }
            catch
            {
                targetNamespace = project.FullName;
            }

            var codeNamespace = new System.CodeDom.CodeNamespace(targetNamespace);
            codeNamespace.Imports.Add(new CodeNamespaceImport("System"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("Gauge.CSharp.Lib.Attribute"));

            var codeTypeDeclaration = new CodeTypeDeclaration(targetClass) {IsClass = true, TypeAttributes = TypeAttributes.Public};
            codeNamespace.Types.Add(codeTypeDeclaration);

            var codeCompileUnit = new CodeCompileUnit();
            codeCompileUnit.Namespaces.Add(codeNamespace);
            var targetFileName = Path.Combine(Path.GetDirectoryName(project.FullName), string.Format("{0}.cs", targetClass));
            using (var streamWriter = new StreamWriter(targetFileName))
            {
                var options = new CodeGeneratorOptions { BracingStyle = "C"};
                codeDomProvider.GenerateCodeFromCompileUnit(codeCompileUnit, streamWriter, options);
            }

            var file = project.ProjectItems.AddFromFile(targetFileName);

            var classes = GetCodeElementsFor(file.FileCodeModel.CodeElements, vsCMElement.vsCMElementClass).ToList();
            return classes.First(element => element.Name == targetClass) as CodeClass;
        }
        
        private static IEnumerable<CodeElement> GetCodeElementsFor(IEnumerable elements, vsCMElement type)
        {
            var codeElements = new List<CodeElement>();

            foreach (CodeElement elem in elements)
            {
                if (elem.Kind == vsCMElement.vsCMElementNamespace)
                {
                    codeElements.AddRange(GetCodeElementsFor(((CodeNamespace)elem).Members, type));
                }
                else if (elem.InfoLocation == vsCMInfoLocation.vsCMInfoLocationExternal)
                {
                    continue;
                }
                else if (elem.IsCodeType)
                {
                    codeElements.AddRange(GetCodeElementsFor(((CodeType)elem).Members, type));
                }
                if (elem.Kind == type)
                    codeElements.Add(elem);
            }

            return codeElements;
        }
        
        public static void NavigateToFunction(CodeFunction function)
        {

            if (!function.ProjectItem.IsOpen)
            {
                function.ProjectItem.Open();
            }

            var startPoint = function.GetStartPoint(vsCMPart.vsCMPartHeader);
            startPoint.TryToShow();
            startPoint.Parent.Selection.MoveToPoint(startPoint);
        }
    }
}