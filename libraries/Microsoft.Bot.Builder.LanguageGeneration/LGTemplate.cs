using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Bot.Builder.LanguageGeneration
{
    /// <summary>
    /// Here is a data model that can easily understanded and used as the context or all kinds of visitors
    /// wether it's evalator, static checker, anayler.. etc.
    /// </summary>
    public class LGTemplate
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LGTemplate"/> class.
        /// </summary>
        /// <param name="parseTree">The parse tree of this template.</param>
        /// <param name="source">Source of this template.</param>
        public LGTemplate(LGFileParser.TemplateDefinitionContext parseTree, string source = "")
        {
            ParseTree = parseTree;
            Source = source;

            Name = ExtractName(parseTree);
            Paramters = ExtractParameters(parseTree);
            Body = ExtractBody(parseTree);
        }

        /// <summary>
        /// Gets name of the template, what's followed by '#' in a LG file.
        /// </summary>
        /// <value>
        /// Name of the template, what's followed by '#' in a LG file.
        /// </value>
        public string Name { get; }

        /// <summary>
        /// Gets paramter list of this template.
        /// </summary>
        /// <value>
        /// Paramter list of this template.
        /// </value>
        public List<string> Paramters { get; }

        /// <summary>
        /// Gets or sets text format of Body of this template. All content except Name and Parameters.
        /// </summary>
        /// <value>
        /// Text format of Body of this template. All content except Name and Parameters.
        /// </value>
        public string Body { get; set; }

        /// <summary>
        /// Gets source of this template, source file path if it's from a certain file.
        /// </summary>
        /// <value>
        /// Source of this template, source file path if it's from a certain file.
        /// </value>
        public string Source { get; }

        /// <summary>
        /// Gets the parse tree of this template.
        /// </summary>
        /// <value>
        /// The parse tree of this template.
        /// </value>
        public LGFileParser.TemplateDefinitionContext ParseTree { get; }

        private string ExtractBody(LGFileParser.TemplateDefinitionContext parseTree) => parseTree.templateBody()?.GetText();

        private string ExtractName(LGFileParser.TemplateDefinitionContext parseTree) => parseTree.templateNameLine().templateName().GetText();

        private List<string> ExtractParameters(LGFileParser.TemplateDefinitionContext parseTree)
        {
            var parameters = parseTree.templateNameLine().parameters();
            if (parameters != null)
            {
                return parameters.IDENTIFIER().Select(param => param.GetText()).ToList();
            }

            return new List<string>();
        }
    }

    public class LGEntityBase
    {
        /// <summary>
        /// Gets or sets file path of the LG file.
        /// </summary>
        /// <value>
        /// File path of the LG file.
        /// </value>
        public string FilePath { get; set; }

        /// <summary>
        /// Gets or sets LG source, like filename, text, inline text.
        /// </summary>
        /// <value>
        /// LG source, like filename, text, inline text.
        /// </value>
        public string Source { get; set; }

        /// <summary>
        /// Gets or sets templates in LG entity.
        /// </summary>
        /// <value>
        /// Templates in LG entity.
        /// </value>
        public List<LGTemplate> Templates { get; set; }

        public void RunStaticCheck()
        {
            var checker = new StaticChecker(this);
            var diagnostics = checker.Check();

            var errors = diagnostics.Where(u => u.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Count != 0)
            {
                throw new Exception(string.Join("\n", errors));
            }
        }
    }

    public class LGFileEntity: LGEntityBase
    {
        public LGFileEntity(string path)
        {
            FilePath = path ?? string.Empty;
            Source = Path.GetFileName(FilePath);
            Templates = LGParser.Parse(File.ReadAllText(FilePath), Source);
            RunStaticCheck();
        }
    }

    public class LGTextEntity: LGEntityBase
    {
        public LGTextEntity(string text, string source = "text")
        {
            FilePath = string.Empty;
            Source = source;
            Templates = LGParser.Parse(text ?? string.Empty, Source);
            RunStaticCheck();
        }
    }
}
