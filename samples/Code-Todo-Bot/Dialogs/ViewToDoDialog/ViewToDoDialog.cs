using System.Collections.Generic;
using System.IO;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Adaptive;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Steps;
using Microsoft.Bot.Builder.LanguageGeneration;

namespace Microsoft.BotBuilderSamples
{
    public class ViewToDoDialog : ComponentDialog
    {
        private TemplateEngine _lgEngine;

        public ViewToDoDialog()
            : base(nameof(ViewToDoDialog))
        {
            _lgEngine = TemplateEngine.FromFiles(
                new string[]
                {
                    Path.Combine(new string[] { ".", "Dialogs", "ViewToDoDialog", "ViewToDoDialog.lg" }),
                    Path.Combine(new string[] { ".", "Dialogs", "RootDialog", "RootDialog.lg" })
                });
            // Create instance of adaptive dialog. 
            var ViewToDoDialog = new AdaptiveDialog(nameof(AdaptiveDialog))
            {
                Generator = new TemplateEngineLanguageGenerator("ViewToDoDialog.lg", _lgEngine),

                Steps = new List<IDialog>()
                {
                    new SendActivity("[View-ToDos]")
                }
            };

            // Add named dialogs to the DialogSet. These names are saved in the dialog state.
            AddDialog(ViewToDoDialog);

            // The initial child Dialog to run.
            InitialDialogId = nameof(AdaptiveDialog);
        }
    }
}
