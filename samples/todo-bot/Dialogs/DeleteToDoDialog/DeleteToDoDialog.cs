﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Adaptive;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Input;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Steps;
using Microsoft.Bot.Builder.Expressions.Parser;

namespace Microsoft.BotBuilderSamples
{
    public class DeleteToDoDialog : ComponentDialog
    {
        public DeleteToDoDialog()
            : base(nameof(DeleteToDoDialog))
        {
            // Create instance of adaptive dialog. 
            var DeleteToDoDialog = new AdaptiveDialog(nameof(AdaptiveDialog))
            {
                Steps = new List<IDialog>()
                {
                    // Handle case where there are no items in todo list
                    new IfCondition()
                    {
                        Condition = new ExpressionEngine().Parse("user.todos == null || count(user.todos) <= 0"),
                        Steps = new List<IDialog>()
                        {
                            new SendActivity("[Delete-Empty-List]"),
                            new SendActivity("[Welcome-Actions]"),
                            new EndDialog()
                        }
                    },
                    // User could have already specified the todo to delete via 
                    // todoTitle as simple machine learned LUIS entity or
                    // todoTitle_patternAny as pattern.any LUIS entity .or.
                    // prebuilt number entity that denotes the position of the todo item in the list .or.
                    // todoIdx machine learned entity that can detect things like first or last etc. 

                    // As a demonstration for this example, use a code step to understand entities returned by LUIS.
                    new CodeStep(GetToDoTitleToDelete),
                    new IfCondition()
                    {
                        Condition = new ExpressionEngine().Parse("turn.todoTitle == null"),
                        Steps = new List<IDialog>()
                        {
                            // First show the current list of Todos
                            new BeginDialog(nameof(ViewToDoDialog)),
                            new TextInput()
                            {
                                Property = "turn.todoTitle",
                                Prompt = new ActivityTemplate("[Get-ToDo-Title-To-Delete]"),
                            }
                        }
                    },
                    new IfCondition()
                    {
                        Condition = new ExpressionEngine().Parse("contains(user.todos, turn.todoTitle) == false"),
                        Steps = new List<IDialog>()
                        {
                            new SendActivity("[Todo-not-found]"),
                            new DeleteProperty()
                            {
                                Property = "turn.todoTitle"
                            },
                            new RepeatDialog()
                        }
                    },
                    new EditArray()
                    {
                        ArrayProperty = "user.todos",
                        ItemProperty = "turn.todoTitle",
                        ChangeType = EditArray.ArrayChangeType.Remove
                    },
                    new SendActivity("[Delete-readBack]"),
                    new EndDialog()
                }
            };

            // Add named dialogs to the DialogSet. These names are saved in the dialog state.
            AddDialog(DeleteToDoDialog);

            // The initial child Dialog to run.
            InitialDialogId = nameof(AdaptiveDialog);
        }

        private async Task<DialogTurnResult> GetToDoTitleToDelete(DialogContext dc, System.Object options)
        {
            // Demonstrates using a custom code step to extract entities and set them in state.
            var todoList = dc.State.GetValue<string[]>("user.todos");
            string todoTitleStr = null;
            string[] numberEntity, todoTitle, todoTitle_patternAny, todoIdx;
            dc.State.TryGetValue("turn.entities.number", out numberEntity);
            dc.State.TryGetValue("turn.entities.todoTitle", out todoTitle);
            dc.State.TryGetValue("turn.entities.todoTitle_patternAny", out todoTitle_patternAny);
            dc.State.TryGetValue("turn.entities.todoIdx", out todoIdx);
            if (numberEntity != null && numberEntity.Length != 0)
            {
                todoTitleStr = todoList[Convert.ToInt16(numberEntity[0]) - 1];
            }
            else if (todoIdx != null && todoIdx.Length != 0)
            {
                if (todoIdx[0] == "first")
                {
                    todoTitleStr = todoList[0];
                }
            }
            else if (todoTitle != null && todoTitle.Length != 0)
            {
                foreach(string todoItem in todoList)
                {
                    if (todoItem == todoTitle[0])
                    {
                        todoTitleStr = todoTitle[0];
                        break;
                    }
                }
            }
            else if (todoTitle_patternAny != null && todoTitle_patternAny.Length != 0)
            {
                foreach (string todoItem in todoList)
                {
                    if (todoItem == todoTitle_patternAny[0])
                    {
                        todoTitleStr = todoTitle_patternAny[0];
                        break;
                    }
                }
            }
            if (todoTitleStr != null)
            {
                dc.State.SetValue("turn.todoTitle", todoTitleStr);
            }
            return new DialogTurnResult(DialogTurnStatus.Complete, options);
        }
    }
}