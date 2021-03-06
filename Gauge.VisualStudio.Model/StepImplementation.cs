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

using EnvDTE;

namespace Gauge.VisualStudio.Model
{
    internal class StepImplementation : Implementation
    {
        private readonly CodeFunction _function;

        public StepImplementation(CodeFunction function, string stepText)
        {
            _function = function;
            StepText = stepText;
        }

        public CodeFunction Function
        {
            get { return _function; }
        }

        public override void NavigateToImplementation(DTE dte)
        {
            Project.NavigateToFunction(Function);
        }
    }
}