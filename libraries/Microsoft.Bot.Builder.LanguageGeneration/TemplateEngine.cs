using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antlr4.Runtime;

namespace Microsoft.Bot.Builder.LanguageGeneration
{
    /// <summary>
    /// The template engine that loads .lg file and eval template based on memory/scope.
    /// </summary>
    public class TemplateEngine
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateEngine"/> class.
        /// Return an empty engine, you can then use AddFile\AddFiles to add files to it,
        /// or you can just use this empty engine to evaluate inline template.
        /// </summary>
        public TemplateEngine()
        {
        }

        /// <summary>
        /// Gets or sets parsed LG templates.
        /// </summary>
        /// <value>
        /// Parsed LG templates.
        /// </value>
        public List<LGTemplate> Templates { get; set; } = new List<LGTemplate>();

        /// <summary>
        /// Create a template engine from files, a shorthand for.
        ///    new TemplateEngine().AddFiles(filePath).
        /// </summary>
        /// <param name="filePaths">paths to LG files.</param>
        /// <returns>Engine created.</returns>
        public static TemplateEngine FromFiles(params string[] filePaths) => new TemplateEngine().AddFiles(filePaths);

        /// <summary>
        /// Create a template engine from text, equivalent to.
        ///    new TemplateEngine.AddText(text).
        /// </summary>
        /// <param name="text">Content of lg file.</param>
        /// <returns>Engine created.</returns>
        public static TemplateEngine FromText(string text) => new TemplateEngine().AddText(text);

        /// <summary>
        /// Load .lg files into template engine
        /// You can add one file, or mutlple file as once
        /// If you have multiple files referencing each other, make sure you add them all at once,
        /// otherwise static checking won't allow you to add it one by one.
        /// </summary>
        /// <param name="filePaths">Paths to .lg files.</param>
        /// <returns>Teamplate engine with parsed files.</returns>
        public TemplateEngine AddFiles(params string[] filePaths)
        {
            var lgfiles = filePaths.Select(filePath => new LGFileEntity(filePath));

            foreach (var lgfile in lgfiles)
            {
                Templates.AddRange(lgfile.Templates);
            }

            return this;
        }

        /// <summary>
        /// Add text as lg file content to template engine.
        /// </summary>
        /// <param name="text">Text content contains lg templates.</param>
        /// <returns>Template engine with the parsed content.</returns>
        public TemplateEngine AddText(string text)
        {
            Templates.AddRange(new LGTextEntity(text).Templates);
            return this;
        }

        /// <summary>
        /// Evaluate a template with given name and scope.
        /// </summary>
        /// <param name="templateName">Template name to be evaluated.</param>
        /// <param name="scope">The state visible in the evaluation.</param>
        /// <param name="methodBinder">Optional methodBinder to extend or override functions.</param>
        /// <returns>Evaluate result.</returns>
        public string EvaluateTemplate(string templateName, object scope, IGetMethod methodBinder = null)
        {
            var evaluator = new Evaluator(Templates, methodBinder);
            return evaluator.EvaluateTemplate(templateName, scope);
        }

        public List<string> AnalyzeTemplate(string templateName)
        {
            var analyzer = new Analyzer(Templates);
            return analyzer.AnalyzeTemplate(templateName);
        }

        /// <summary>
        /// Use to evaluate an inline template str.
        /// </summary>
        /// <param name="inlineStr">inline string which will be evaluated.</param>
        /// <param name="scope">scope object or JToken.</param>
        /// <param name="methodBinder">input method.</param>
        /// <returns>Evaluate result.</returns>
        public string Evaluate(string inlineStr, object scope, IGetMethod methodBinder = null)
        {
            // wrap inline string with "# name and -" to align the evaluation process
            var fakeTemplateId = "__temp__";
            inlineStr = !inlineStr.Trim().StartsWith("```") && inlineStr.IndexOf('\n') >= 0
                   ? "```" + inlineStr + "```" : inlineStr;
            var wrappedStr = $"# {fakeTemplateId} \r\n - {inlineStr}";

            var lgtext = new LGTextEntity(wrappedStr, "inline text");
            lgtext.Templates.AddRange(Templates);
            lgtext.RunStaticCheck();

            var evaluator = new Evaluator(lgtext.Templates, methodBinder);
            return evaluator.EvaluateTemplate(fakeTemplateId, scope);
        }
    }
}
