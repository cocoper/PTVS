﻿// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.ComponentModel.Composition;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.IncrementalSearch;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Editor {
    /// <summary>
    /// Provides centralized access to services used by the editor.
    /// </summary>
    /// <remarks>
    /// MEF services provided by VS should be imported eagerly using the
    /// [Import] attribute on a public field.
    /// 
    /// Traditional services should be lazily imported on first use, and
    /// uses should be audited to ensure they all occur on the UI thread.
    /// 
    /// Services provided by PTVS should be lazily loaded on first access.
    /// Otherwise, we may end up with circular imports in MEF composition.
    /// </remarks>
    [Export]
    sealed class PythonEditorServices {
        [ImportingConstructor]
        public PythonEditorServices([Import(typeof(SVsServiceProvider))] IServiceProvider site) {
            Site = site;
            ComponentModel = Site.GetComponentModel();
            _errorTaskProvider = new Lazy<ErrorTaskProvider>(CreateTaskProvider<ErrorTaskProvider>);
            _commentTaskProvider = new Lazy<CommentTaskProvider>(CreateTaskProvider<CommentTaskProvider>);
            _unresolvedImportSquiggleProvider = new Lazy<UnresolvedImportSquiggleProvider>(CreateImportSquiggleProvider);
        }

        public readonly IServiceProvider Site;

        #region PythonToolsService

        private PythonToolsService _python;

        internal void SetPythonToolsService(PythonToolsService service) {
            if (_python != null) {
                throw new InvalidOperationException("Multiple services created");
            }
            _python = service;
        }

        internal PythonToolsService TryGetPythonToolsService() {
            return _python = Site.GetUIThread().Invoke(() => Site.GetPythonToolsService());
        }

        public PythonToolsService Python => _python ?? TryGetPythonToolsService();

        #endregion

        public PythonTextBufferInfo GetBufferInfo(ITextBuffer textBuffer) {
            return PythonTextBufferInfo.ForBuffer(this, textBuffer);
        }

        public IComponentModel ComponentModel { get; }

        [Import]
        public IClassificationTypeRegistryService ClassificationTypeRegistryService;
        [Import]
        public IContentTypeRegistryService ContentTypeRegistryService;

        [Import]
        private Lazy<AnalysisEntryService> _analysisEntryService = null;
        public AnalysisEntryService AnalysisEntryService => _analysisEntryService.Value;

        [Import]
        private Lazy<IVsEditorAdaptersFactoryService> _editorAdaptersFactoryService = null;
        public IVsEditorAdaptersFactoryService EditorAdaptersFactoryService => _editorAdaptersFactoryService.Value;

        [Import]
        public ICompletionBroker CompletionBroker = null;

        [Import(AllowDefault = true)]
        public IEditorOptionsFactoryService EditorOptionsFactoryService = null;

        [Import]
        public IEditorOperationsFactoryService EditOperationsFactory = null;

        [Import]
        public ISignatureHelpBroker SignatureHelpBroker = null;

        [Import]
        public IQuickInfoBroker QuickInfoBroker = null;

        [Import]
        public IPeekBroker PeekBroker = null;

        [Import]
        public IIncrementalSearchFactoryService IncrementalSearch = null;

        [Import]
        public ITextMarkerProviderFactory TextMarkerProviderFactory = null;

        [Import]
        public ITextBufferUndoManagerProvider UndoManagerFactory = null;

        public IVsTextManager2 VsTextManager2 => (IVsTextManager2)Site.GetService(typeof(SVsTextManager));

        #region Task Providers

        private readonly Lazy<ErrorTaskProvider> _errorTaskProvider;
        public ErrorTaskProvider ErrorTaskProvider => _errorTaskProvider.Value;
        public ErrorTaskProvider MaybeErrorTaskProvider => _errorTaskProvider.IsValueCreated ? _errorTaskProvider.Value : null;

        private readonly Lazy<CommentTaskProvider> _commentTaskProvider;
        public CommentTaskProvider CommentTaskProvider => _commentTaskProvider.Value;
        public CommentTaskProvider MaybeCommentTaskProvider => _commentTaskProvider.IsValueCreated ? _commentTaskProvider.Value : null;

        private readonly Lazy<UnresolvedImportSquiggleProvider> _unresolvedImportSquiggleProvider;
        public UnresolvedImportSquiggleProvider UnresolvedImportSquiggleProvider => _unresolvedImportSquiggleProvider.Value;
        public UnresolvedImportSquiggleProvider MaybeUnresolvedImportSquiggleProvider => _unresolvedImportSquiggleProvider.IsValueCreated ? _unresolvedImportSquiggleProvider.Value : null;

        private T CreateTaskProvider<T>() where T : class {
            if (VsProjectAnalyzer.SuppressTaskProvider) {
                return null;
            }
            return (T)Site.GetService(typeof(T));
        }

        private UnresolvedImportSquiggleProvider CreateImportSquiggleProvider() {
            if (VsProjectAnalyzer.SuppressTaskProvider) {
                return null;
            }
            var errorProvider = ErrorTaskProvider;
            if (errorProvider == null) {
                return null;
            }
            return new UnresolvedImportSquiggleProvider(Site, errorProvider);
        }

        #endregion
    }
}
