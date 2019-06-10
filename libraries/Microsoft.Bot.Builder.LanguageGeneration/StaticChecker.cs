﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Microsoft.Bot.Builder.Expressions.Parser;

namespace Microsoft.Bot.Builder.LanguageGeneration
{
    public class StaticChecker : LGFileParserBaseVisitor<List<Diagnostic>>
    {
        private Dictionary<string, LGTemplate> templateMap = new Dictionary<string, LGTemplate>();

        private LGEntityBase lgentity;

        public StaticChecker(LGEntityBase lgentity)
        {
            this.lgentity = lgentity;
        }

        public List<LGTemplate> Templates => lgentity.Templates;

        /// <summary>
        /// Return error messaages list.
        /// </summary>
        /// <returns>report result.</returns>
        public List<Diagnostic> Check()
        {
            var result = new List<Diagnostic>();

            // check dup first
            var duplicatedTemplates = Templates
                                      .GroupBy(t => t.Name)
                                      .Where(g => g.Count() > 1)
                                      .ToList();

            if (duplicatedTemplates.Count > 0)
            {
                duplicatedTemplates.ForEach(g =>
                {
                    var name = g.Key;
                    var sources = string.Join(":", g.Select(x => x.Source));

                    var msg = $"Duplicated definitions found for template: {name} in {sources}";
                    result.Add(BuildLGDiagnostic(msg));
                });

                return result;
            }

            // Covert to dict should be fine after checking dup
            templateMap = Templates.ToDictionary(t => t.Name);

            if (Templates.Count == 0)
            {
                result.Add(BuildLGDiagnostic(
                    "File must have at least one template definition ",
                    DiagnosticSeverity.Warning));
            }

            Templates.ForEach(t =>
            {
                result.AddRange(Visit(t.ParseTree));
            });

            return result;
        }

        public override List<Diagnostic> VisitTemplateDefinition([NotNull] LGFileParser.TemplateDefinitionContext context)
        {
            var result = new List<Diagnostic>();
            var templateName = context.templateNameLine().templateName().GetText();

            if (context.templateBody() == null)
            {
                result.Add(BuildLGDiagnostic($"There is no template body in template {templateName}", context: context.templateNameLine()));
            }
            else
            {
                result.AddRange(Visit(context.templateBody()));
            }

            var parameters = context.templateNameLine().parameters();
            if (parameters != null)
            {
                if (parameters.CLOSE_PARENTHESIS() == null
                       || parameters.OPEN_PARENTHESIS() == null)
                {
                    result.Add(BuildLGDiagnostic($"parameters: {parameters.GetText()} format error", context: context.templateNameLine()));
                }

                var invalidSeperateCharacters = parameters.INVALID_SEPERATE_CHAR();
                if (invalidSeperateCharacters != null
                    && invalidSeperateCharacters.Length > 0)
                {
                    result.Add(BuildLGDiagnostic("Parameters for templates must be separated by comma.", context: context.templateNameLine()));
                }
            }

            return result;
        }

        public override List<Diagnostic> VisitNormalTemplateBody([NotNull] LGFileParser.NormalTemplateBodyContext context)
        {
            var result = new List<Diagnostic>();

            foreach (var templateStr in context.normalTemplateString())
            {
                var item = Visit(templateStr);
                result.AddRange(item);
            }

            return result;
        }

        public override List<Diagnostic> VisitIfElseBody([NotNull] LGFileParser.IfElseBodyContext context)
        {
            var result = new List<Diagnostic>();

            var ifRules = context.ifElseTemplateBody().ifConditionRule();
            for (var idx = 0; idx < ifRules.Length; idx++)
            {
                var conditionNode = ifRules[idx].ifCondition();
                var ifExpr = conditionNode.IF() != null;
                var elseIfExpr = conditionNode.ELSEIF() != null;
                var elseExpr = conditionNode.ELSE() != null;

                var node = ifExpr ? conditionNode.IF() :
                           elseIfExpr ? conditionNode.ELSEIF() :
                           conditionNode.ELSE();

                if (node.GetText().Count(u => u == ' ') > 1)
                {
                    result.Add(BuildLGDiagnostic($"At most 1 whitespace is allowed between IF/ELSEIF/ELSE and :. expression: '{context.ifElseTemplateBody().GetText()}", context: conditionNode));
                }

                if (idx == 0 && !ifExpr)
                {
                    result.Add(BuildLGDiagnostic($"condition is not start with if: '{context.ifElseTemplateBody().GetText()}'", DiagnosticSeverity.Warning, conditionNode));
                }

                if (idx > 0 && ifExpr)
                {
                    result.Add(BuildLGDiagnostic($"condition can't have more than one if: '{context.ifElseTemplateBody().GetText()}'", context: conditionNode));
                }

                if (idx == ifRules.Length - 1 && !elseExpr)
                {
                    result.Add(BuildLGDiagnostic($"condition is not end with else: '{context.ifElseTemplateBody().GetText()}'", DiagnosticSeverity.Warning, conditionNode));
                }

                if (idx > 0 && idx < ifRules.Length - 1 && !elseIfExpr)
                {
                    result.Add(BuildLGDiagnostic($"only elseif is allowed in middle of condition: '{context.ifElseTemplateBody().GetText()}'", context: conditionNode));
                }

                // check rule should should with one and only expression
                if (!elseExpr)
                {
                    if (ifRules[idx].ifCondition().EXPRESSION().Length != 1)
                    {
                        result.Add(BuildLGDiagnostic($"if and elseif should followed by one valid expression: '{ifRules[idx].GetText()}'", context: conditionNode));
                    }
                    else
                    {
                        result.AddRange(CheckExpression(ifRules[idx].ifCondition().EXPRESSION(0).GetText(), conditionNode));
                    }
                }
                else
                {
                    if (ifRules[idx].ifCondition().EXPRESSION().Length != 0)
                    {
                        result.Add(BuildLGDiagnostic($"else should not followed by any expression: '{ifRules[idx].GetText()}'", context: conditionNode));
                    }
                }

                if (ifRules[idx].normalTemplateBody() != null)
                {
                    result.AddRange(Visit(ifRules[idx].normalTemplateBody()));
                }
                else
                {
                    result.Add(BuildLGDiagnostic($"no normal template body in condition block: '{ifRules[idx].GetText()}'", context: conditionNode));
                }
            }

            return result;
        }

        public override List<Diagnostic> VisitSwitchCaseBody([NotNull] LGFileParser.SwitchCaseBodyContext context)
        {
            var result = new List<Diagnostic>();
            var switchCaseRules = context.switchCaseTemplateBody().switchCaseRule();
            var length = switchCaseRules.Length;
            for (var idx = 0; idx < length; idx++)
            {
                var switchCaseNode = switchCaseRules[idx].switchCaseStat();
                var switchExpr = switchCaseNode.SWITCH() != null;
                var caseExpr = switchCaseNode.CASE() != null;
                var defaultExpr = switchCaseNode.DEFAULT() != null;
                var node = switchExpr ? switchCaseNode.SWITCH() :
                           caseExpr ? switchCaseNode.CASE() :
                           switchCaseNode.DEFAULT();

                if (node.GetText().Count(u => u == ' ') > 1)
                {
                    result.Add(BuildLGDiagnostic($"At most 1 whitespace is allowed between SWITCH/CASE/DEFAULT and :. expression: '{context.switchCaseTemplateBody().GetText()}", context: switchCaseNode));
                }

                if (idx == 0 && !switchExpr)
                {
                    result.Add(BuildLGDiagnostic($"control flow is not start with switch: '{context.switchCaseTemplateBody().GetText()}'", context: switchCaseNode));
                }

                if (idx > 0 && switchExpr)
                {
                    result.Add(BuildLGDiagnostic($"control flow can not have more than one switch statement: '{context.switchCaseTemplateBody().GetText()}'", context: switchCaseNode));
                }

                if (idx > 0 && idx < length - 1 && !caseExpr)
                {
                    result.Add(BuildLGDiagnostic($"only case statement is allowed in the middle of control flow: '{context.switchCaseTemplateBody().GetText()}'", context: switchCaseNode));
                }

                if (idx == length - 1 && (caseExpr || defaultExpr))
                {
                    if (caseExpr)
                    {
                        result.Add(BuildLGDiagnostic($"control flow is not ending with default statement: '{context.switchCaseTemplateBody().GetText()}'", DiagnosticSeverity.Warning, switchCaseNode));
                    }
                    else
                    {
                        if (length == 2)
                        {
                            result.Add(BuildLGDiagnostic($"control flow should have at least one case statement: '{context.switchCaseTemplateBody().GetText()}'", DiagnosticSeverity.Warning, switchCaseNode));
                        }
                    }
                }

                if (switchExpr || caseExpr)
                {
                    if (switchCaseNode.EXPRESSION().Length != 1)
                    {
                        result.Add(BuildLGDiagnostic($"switch and case should followed by one valid expression: '{context.switchCaseTemplateBody().GetText()}'", context: switchCaseNode));
                    }
                    else
                    {
                        result.AddRange(CheckExpression(switchCaseNode.EXPRESSION(0).GetText(), switchCaseNode));
                    }
                }
                else
                {
                    if (switchCaseNode.EXPRESSION().Length != 0 || switchCaseNode.TEXT().Length != 0)
                    {
                        result.Add(BuildLGDiagnostic($"default should not followed by any expression or any text: '{context.switchCaseTemplateBody().GetText()}'", context: switchCaseNode));
                    }
                }

                if (caseExpr || defaultExpr)
                {
                    if (switchCaseRules[idx].normalTemplateBody() != null)
                    {
                        result.AddRange(Visit(switchCaseRules[idx].normalTemplateBody()));
                    }
                    else
                    {
                        result.Add(BuildLGDiagnostic($"no normal template body in case or default block: '{context.switchCaseTemplateBody().GetText()}'", context: switchCaseNode));
                    }
                }
            }

            return result;
        }

        public override List<Diagnostic> VisitNormalTemplateString([NotNull] LGFileParser.NormalTemplateStringContext context)
        {
            var result = new List<Diagnostic>();

            foreach (ITerminalNode node in context.children)
            {
                switch (node.Symbol.Type)
                {
                    case LGFileParser.INVALID_ESCAPE:
                        result.Add(BuildLGDiagnostic($"escape character {node.GetText()} is invalid", context: context));
                        break;
                    case LGFileParser.TEMPLATE_REF:
                        result.AddRange(CheckTemplateRef(node.GetText(), context));
                        break;
                    case LGFileParser.EXPRESSION:
                        result.AddRange(CheckExpression(node.GetText(), context));
                        break;
                    case LGFileLexer.MULTI_LINE_TEXT:
                        result.AddRange(CheckMultiLineText(node.GetText(), context));
                        break;
                    case LGFileLexer.TEXT:
                        result.AddRange(CheckText(node.GetText(), context));
                        break;
                    default:
                        break;
                }
            }

            return result;
        }

        public List<Diagnostic> CheckTemplateRef(string exp, ParserRuleContext context)
        {
            var result = new List<Diagnostic>();

            exp = exp.TrimStart('[').TrimEnd(']').Trim();

            var argsStartPos = exp.IndexOf('(');

            // Do have args
            if (argsStartPos > 0)
            {
                // EvaluateTemplate all arguments using ExpressoinEngine
                var argsEndPos = exp.LastIndexOf(')');
                if (argsEndPos < 0 || argsEndPos < argsStartPos + 1)
                {
                    result.Add(BuildLGDiagnostic($"Not a valid template ref: {exp}", context: context));
                }
                else
                {
                    var templateName = exp.Substring(0, argsStartPos);
                    if (!templateMap.ContainsKey(templateName))
                    {
                        result.Add(BuildLGDiagnostic($"[{templateName}] template not found", context: context));
                    }
                    else
                    {
                        var argsNumber = exp.Substring(argsStartPos + 1, argsEndPos - argsStartPos - 1).Split(',').Length;
                        result.AddRange(CheckTemplateParameters(templateName, argsNumber, context));
                    }
                }
            }
            else
            {
                if (!templateMap.ContainsKey(exp))
                {
                    result.Add(BuildLGDiagnostic($"[{exp}] template not found", context: context));
                }
            }

            return result;
        }

        private List<Diagnostic> CheckMultiLineText(string exp, ParserRuleContext context)
        {
            var result = new List<Diagnostic>();

            // remove ``` ```
            exp = exp.Substring(3, exp.Length - 6);
            var reg = @"@\{[^{}]+\}";
            var mc = Regex.Matches(exp, reg);

            foreach (Match match in mc)
            {
                result.AddRange(CheckExpression(match.Value, context));
            }

            return result;
        }

        private List<Diagnostic> CheckText(string exp, ParserRuleContext context)
        {
            var result = new List<Diagnostic>();

            if (exp.StartsWith("```"))
            {
                result.Add(BuildLGDiagnostic("Multi line variation must be enclosed in ```", context: context));
            }

            return result;
        }

        private List<Diagnostic> CheckTemplateParameters(string templateName, int argsNumber, ParserRuleContext context)
        {
            var result = new List<Diagnostic>();
            var parametersNumber = templateMap[templateName].Paramters.Count;

            if (argsNumber != parametersNumber)
            {
                result.Add(BuildLGDiagnostic($"Arguments count mismatch for template ref {templateName}, expected {parametersNumber}, actual {argsNumber}", context: context));
            }

            return result;
        }

        private List<Diagnostic> CheckExpression(string exp, ParserRuleContext context)
        {
            var result = new List<Diagnostic>();
            exp = exp.TrimStart('@').TrimStart('{').TrimEnd('}');
            try
            {
                new ExpressionEngine(new GetMethodExtensions(new Evaluator(this.Templates, null)).GetMethodX).Parse(exp);
            }
            catch (Exception e)
            {
                result.Add(BuildLGDiagnostic(e.Message + $" in expression `{exp}`", context: context));
                return result;
            }

            return result;
        }

        /// <summary>
        /// Build LG diagnostic with antlr tree node context.
        /// </summary>
        /// <param name="message">error/warning message. <see cref="Diagnostic.Message"/>.</param>
        /// <param name="severity">diagnostic Severity <see cref="DiagnosticSeverity"/> to get more info.</param>
        /// <param name="context">the parsed tree node context of the diagnostic.</param>
        /// <returns>new Diagnostic object.</returns>
        private Diagnostic BuildLGDiagnostic(
            string message,
            DiagnosticSeverity severity = DiagnosticSeverity.Error,
            ParserRuleContext context = null)
        {
            var startPosition = context == null ? new Position(0, 0) : new Position(context.Start.Line - 1, context.Start.Column);
            var stopPosition = context == null ? new Position(0, 0) : new Position(context.Stop.Line - 1, context.Stop.Column + context.Stop.Text.Length);
            var range = new Range(startPosition, stopPosition);
            message = "source: " + lgentity.Source + ", error message: " + message;
            return new Diagnostic(range, message, severity);
        }
    }
}
